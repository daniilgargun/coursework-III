using ScheduleViewer.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Linq;
using HtmlAgilityPack;
using System.Globalization;
using System.Diagnostics;
using System.Threading;
using System.IO;
using Microsoft.EntityFrameworkCore;
using ScheduleViewer.Data;

namespace ScheduleViewer.Services
{
    public class ScheduleParser
    {
        private readonly HttpClient _httpClient;
        private readonly string _defaultUrl = "https://bartc.by/index.php/obuchayushchemusya/dnevnoe-otdelenie/tekushchee-raspisanie";
        
        public ScheduleParser()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(60); // Увеличиваем тайм-аут до 60 секунд
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36"); // Добавляем User-Agent для имитации браузера
        }
        
        /// <summary>
        /// Загрузка расписания с сервера и парсинг данных
        /// </summary>
        public async Task<List<Schedule>> ParseScheduleAsync(string url = null)
        {
            try
            {
                // Используем URL из параметра или URL по умолчанию
                url = string.IsNullOrWhiteSpace(url) ? _defaultUrl : url;
                
                Logger.Log($"Загрузка расписания с URL: {url}", LogLevel.Info);
                
                // Делаем до 3 попыток загрузки страницы
                string html = string.Empty;
                int maxAttempts = 3;
                
                for (int attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    try
                    {
                        // Загрузка HTML страницы
                        Logger.Log($"Попытка {attempt} загрузки страницы с URL: {url}", LogLevel.Debug);
                        
                        // Используем HttpClient с правильной кодировкой для русского языка
                        var response = await _httpClient.GetAsync(url);
                        response.EnsureSuccessStatusCode();
                        
                        // Получаем байты ответа для правильной обработки кодировки
                        var bytes = await response.Content.ReadAsByteArrayAsync();
                        
                        // Пробуем определить кодировку из заголовка Content-Type
                        var contentType = response.Content.Headers.ContentType?.CharSet;
                        System.Text.Encoding encoding = null;
                        
                        if (!string.IsNullOrEmpty(contentType))
                        {
                            try 
                            {
                                encoding = System.Text.Encoding.GetEncoding(contentType);
                                Logger.Log($"Используется кодировка из заголовка: {contentType}", LogLevel.Debug);
                            }
                            catch (Exception ex)
                            {
                                Logger.Log($"Не удалось использовать кодировку из заголовка: {ex.Message}", LogLevel.Warning);
                            }
                        }
                        
                        // Если не удалось определить кодировку из заголовка, пробуем разные варианты
                        if (encoding == null)
                        {
                            // Пробуем несколько разных кодировок, начиная с наиболее вероятных
                            var encodings = new System.Text.Encoding[] 
                            {
                                System.Text.Encoding.UTF8,
                                System.Text.Encoding.GetEncoding("windows-1251"),
                                System.Text.Encoding.GetEncoding("iso-8859-5"),
                                System.Text.Encoding.GetEncoding("koi8-r")
                            };
                            
                            string bestHtml = null;
                            
                            // Проверяем каждую кодировку и выбираем ту, которая дает наиболее читаемый результат
                            foreach (var testEncoding in encodings)
                            {
                                try 
                                {
                                    string testHtml = testEncoding.GetString(bytes);
                                    Logger.Log($"Проверка кодировки {testEncoding.WebName}", LogLevel.Debug);
                                    
                                    // Проверяем наличие русских букв и ключевых слов
                                    if (ContainsRussianText(testHtml) && 
                                        (testHtml.Contains("расписание") || testHtml.Contains("группа") || 
                                         testHtml.Contains("преподаватель") || testHtml.Contains("аудитория")))
                                    {
                                        Logger.Log($"Найдена подходящая кодировка: {testEncoding.WebName}", LogLevel.Info);
                                        bestHtml = testHtml;
                                        encoding = testEncoding;
                                        break;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Logger.Log($"Ошибка при проверке кодировки {testEncoding.WebName}: {ex.Message}", LogLevel.Warning);
                                }
                            }
                            
                            // Если нашли читаемый вариант
                            if (bestHtml != null)
                            {
                                html = bestHtml;
                            }
                            else 
                            {
                                // Если не нашли, используем UTF-8 как последнюю попытку
                                html = System.Text.Encoding.UTF8.GetString(bytes);
                                Logger.Log("Не удалось определить правильную кодировку, используем UTF-8", LogLevel.Warning);
                            }
                        }
                        else 
                        {
                            // Используем найденную кодировку
                            html = encoding.GetString(bytes);
                        }
                        
                        if (!string.IsNullOrWhiteSpace(html))
                        {
                            Logger.Log($"Успешно получен HTML, длина: {html.Length} символов, кодировка: {encoding?.WebName ?? "неизвестная"}", LogLevel.Debug);
                            break; // Успешно получили данные
                        }
                            
                        Logger.Log($"Попытка {attempt}: получен пустой HTML, повторная попытка...", LogLevel.Warning);
                        await Task.Delay(1000); // Подождем секунду перед повторной попыткой
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Попытка {attempt} вызвала исключение: {ex.GetType().Name}: {ex.Message}", LogLevel.Warning);
                        
                        if (attempt == maxAttempts)
                        {
                            Logger.Log("Исчерпаны все попытки загрузки страницы", LogLevel.Error);
                            throw; // Пробрасываем исключение после последней попытки
                        }
                            
                        Logger.Log($"Попытка {attempt} не удалась: {ex.Message}, повторная попытка через 2 секунды...", LogLevel.Warning);
                        await Task.Delay(2000); // Подождем 2 секунды перед повторной попыткой
                    }
                }
                
                if (string.IsNullOrWhiteSpace(html))
                {
                    Logger.Log("Получен пустой HTML после всех попыток", LogLevel.Error);
                    throw new Exception("Не удалось получить данные с сайта БТК. Пожалуйста, проверьте подключение к интернету и повторите попытку.");
                }
                
                // Сохраняем часть HTML для диагностики
                var htmlPreview = html.Length > 500 ? html.Substring(0, 500) + "..." : html;
                Logger.Log($"Начало полученного HTML: {htmlPreview}", LogLevel.Debug);
                
                // Проверяем, содержит ли страница нужные данные
                if (!html.Contains("table"))
                {
                    Logger.Log("HTML не содержит таблиц", LogLevel.Error);
                    throw new Exception("Полученная страница не содержит таблиц с расписанием. Возможно, изменилась структура сайта.");
                }
                
                if (!html.Contains("расписание", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Log("HTML не содержит ключевого слова 'расписание'", LogLevel.Error);
                    throw new Exception("Полученная страница не содержит ключевого слова 'расписание'. Возможно, сайт вернул страницу с ошибкой или изменил структуру.");
                }
                
                // Парсинг данных
                var schedules = ParseScheduleHtml(html);
                
                if (schedules.Count == 0)
                {
                    Logger.Log("Расписание не найдено на странице", LogLevel.Error);
                    throw new Exception("Не удалось найти расписание на странице. Возможно, сайт временно недоступен или изменил структуру.");
                }
                
                Logger.Log($"Обработано {schedules.Count} записей расписания", LogLevel.Info);
                return schedules;
            }
            catch (HttpRequestException ex)
            {
                Logger.LogException(ex, "HTTP запрос при загрузке расписания");
                throw new Exception($"Не удалось загрузить данные с сайта БТК. Проверьте подключение к интернету. Ошибка: {ex.Message}", ex);
            }
            catch (TaskCanceledException ex)
            {
                Logger.LogException(ex, "Тайм-аут при загрузке расписания");
                throw new Exception("Превышено время ожидания при загрузке расписания с сайта БТК. Проверьте подключение к интернету или попробуйте позже.", ex);
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Загрузка расписания");
                throw new Exception($"Ошибка при обработке расписания: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// Парсинг HTML-страницы расписания
        /// </summary>
        private List<Schedule> ParseScheduleHtml(string html)
        {
            var schedules = new List<Schedule>();
            
            try
            {
                Logger.Log("Начинаем парсинг HTML страницы", LogLevel.Info);
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);
                
                // Сохраним HTML для отладки, если возникают проблемы
                string debugHtmlPath = Path.Combine(Logger.GetLogDirectory(), "last_html_debug.html");
                try {
                    File.WriteAllText(debugHtmlPath, html);
                    Logger.Log($"Сохранен HTML для отладки: {debugHtmlPath}", LogLevel.Debug);
                } catch (Exception) { /* Игнорируем ошибки при сохранении */ }
                
                // Проверяем, содержит ли страница сообщение об ошибке или технических работах
                var errorNodes = htmlDoc.DocumentNode.SelectNodes("//div[contains(@class,'error')]") ?? 
                                htmlDoc.DocumentNode.SelectNodes("//div[contains(@class,'message')]") ??
                                htmlDoc.DocumentNode.SelectNodes("//div[contains(text(),'ошибка')]") ??
                                htmlDoc.DocumentNode.SelectNodes("//div[contains(text(),'технические работы')]");
                
                if (errorNodes != null && errorNodes.Any())
                {
                    var errorText = errorNodes.First().InnerText.Trim();
                    Logger.Log($"Найдено сообщение об ошибке на странице: {errorText}", LogLevel.Warning);
                    throw new Exception($"Сайт БТК вернул страницу с ошибкой: {errorText}");
                }
                
                // Проверяем, не запрашивает ли сайт вход в аккаунт
                var loginNodes = htmlDoc.DocumentNode.SelectNodes("//form[contains(@action,'login')]") ??
                                htmlDoc.DocumentNode.SelectNodes("//input[@type='password']");
                if (loginNodes != null && loginNodes.Any())
                {
                    Logger.Log("Сайт БТК запрашивает авторизацию", LogLevel.Warning);
                    throw new Exception("Сайт БТК требует авторизацию. Возможно, страница расписания доступна только авторизованным пользователям.");
                }
                
                // Находим все таблицы на странице и проверяем их содержимое
                var tables = htmlDoc.DocumentNode.SelectNodes("//table");
                
                if (tables == null || !tables.Any())
                {
                    Logger.Log("На странице не найдены таблицы", LogLevel.Error);
                    
                    // Ищем div с классом item-page
                    var itemPage = htmlDoc.DocumentNode.SelectSingleNode("//div[@class='item-page']");
                    if (itemPage != null)
                    {
                        var contentText = itemPage.InnerText.Trim();
                        var shortenedText = contentText.Length > 200 ? contentText.Substring(0, 200) + "..." : contentText;
                        Logger.Log($"Найден div с классом item-page, но без таблиц. Содержимое: {shortenedText}", LogLevel.Warning);
                        
                        if (contentText.Contains("обновляется", StringComparison.OrdinalIgnoreCase) ||
                            contentText.Contains("скоро появится", StringComparison.OrdinalIgnoreCase) ||
                            contentText.Contains("будет доступно", StringComparison.OrdinalIgnoreCase))
                        {
                            throw new Exception("Расписание на сайте БТК в процессе обновления. Пожалуйста, повторите попытку позже.");
                        }
                    }
                    
                    return schedules;
                }
                
                Logger.Log($"Найдено таблиц: {tables.Count}", LogLevel.Info);

                // Словари для хранения уникальных объектов
                var groups = new Dictionary<string, Models.Group>();
                var subjects = new Dictionary<string, Subject>();
                var teachers = new Dictionary<string, Teacher>();
                var classrooms = new Dictionary<string, Classroom>();
                
                // Просмотрим все таблицы и выберем наиболее подходящую
                var bestTable = FindBestTable(tables);
                if (bestTable == null)
                {
                    Logger.Log("Не найдена подходящая таблица с расписанием", LogLevel.Error);
                    return schedules;
                }
                
                Logger.Log("Найдена таблица расписания", LogLevel.Info);
                
                // Попытаемся найти дату
                string currentDay = string.Empty;
                DateTime parsedDate = DateTime.Today;
                
                // Поиск заголовка с датой перед таблицей
                var previousElement = bestTable.PreviousSibling;
                bool dateFound = false;
                while (previousElement != null && !dateFound)
                {
                    if (previousElement.Name == "h3" || previousElement.Name == "h2" || 
                        previousElement.Name == "strong" || previousElement.Name == "p")
                    {
                        var dateText = previousElement.InnerText.Trim();
                        if (!string.IsNullOrEmpty(dateText))
                        {
                            Logger.Log($"Найден возможный заголовок с датой: '{dateText}'", LogLevel.Debug);
                            
                            if (TryParseDate(dateText, out parsedDate))
                            {
                                currentDay = dateText;
                                dateFound = true;
                                Logger.Log($"Успешно распознана дата: {currentDay}, распознано как {parsedDate.ToShortDateString()}", LogLevel.Info);
                                break;
                            }
                        }
                    }
                    previousElement = previousElement.PreviousSibling;
                }
                
                if (!dateFound)
                {
                    Logger.Log($"Не найден заголовок с датой, будет использована текущая дата: {parsedDate.ToShortDateString()}", LogLevel.Info);
                }
                
                // Получаем строки таблицы
                var rows = bestTable.SelectNodes(".//tr");
                if (rows == null || rows.Count <= 1)
                {
                    Logger.Log("Таблица не содержит достаточно строк", LogLevel.Warning);
                    return schedules;
                }
                
                Logger.Log($"В таблице найдено строк: {rows.Count}", LogLevel.Debug);
                
                // Анализируем структуру таблицы
                var columnIndices = DetectTableColumns(rows[0]);
                
                // Проверяем результаты анализа
                if (columnIndices.Count == 0)
                {
                    Logger.Log("Не удалось определить структуру таблицы на основе заголовка", LogLevel.Warning);
                    
                    // Попробуем анализировать данные строки таблицы
                    if (rows.Count > 1)
                    {
                        columnIndices = InferTableStructure(rows.Skip(1).Take(10).ToList());
                    }
                }
                
                if (columnIndices.Count == 0)
                {
                    Logger.Log("Не удалось определить структуру таблицы", LogLevel.Error);
                    return schedules;
                }
                
                Logger.Log($"Определена структура таблицы: {string.Join(", ", columnIndices.Select(kv => $"{kv.Key}={kv.Value}"))}", LogLevel.Info);
                
                // Проверяем наличие минимально необходимых колонок
                if ((!columnIndices.ContainsKey("group") && !columnIndices.ContainsKey("teacher")) || 
                    !columnIndices.ContainsKey("subject"))
                {
                    Logger.Log("Отсутствуют минимально необходимые колонки (группа/преподаватель и предмет)", LogLevel.Error);
                    return schedules;
                }
                
                // Обрабатываем данные строки таблицы
                for (int i = 1; i < rows.Count; i++)
                {
                    var row = rows[i];
                    var cells = row.SelectNodes(".//td");
                    if (cells == null || cells.Count < 3) continue;
                    
                    try
                    {
                        // Проверяем, содержит ли первая ячейка дату
                        if (cells.Count > 0)
                        {
                            var firstCellText = cells[0].InnerText.Trim();
                            if (!string.IsNullOrEmpty(firstCellText) && TryParseDate(firstCellText, out DateTime rowDate))
                            {
                                parsedDate = rowDate;
                                currentDay = firstCellText;
                                Logger.Log($"Обнаружена дата в строке: {parsedDate.ToShortDateString()}", LogLevel.Debug);
                            }
                        }
                        
                        // Получаем группу или преподавателя
                        string groupText = "";
                        string teacherText = "";
                        
                        if (columnIndices.TryGetValue("group", out int groupIndex) && 
                            groupIndex >= 0 && cells.Count > groupIndex)
                        {
                            groupText = cells[groupIndex].InnerText.Trim();
                        }
                        
                        if (columnIndices.TryGetValue("teacher", out int teacherIdx) && 
                            teacherIdx >= 0 && cells.Count > teacherIdx)
                        {
                            teacherText = cells[teacherIdx].InnerText.Trim();
                        }
                        
                        // Должна быть указана либо группа, либо преподаватель
                        if (string.IsNullOrEmpty(groupText) && string.IsNullOrEmpty(teacherText))
                        {
                            continue;
                        }
                        
                        // Получаем номер урока/пары
                        int lessonNumber = 0;
                        string lessonNumberText = "";
                        
                        if (columnIndices.TryGetValue("lesson", out int lessonIndex) && 
                            lessonIndex >= 0 && cells.Count > lessonIndex)
                        {
                            lessonNumberText = cells[lessonIndex].InnerText.Trim();
                            
                            // Разные форматы номера пары: "1", "1 пара", "1 (8:30-10:00)"
                            var match = System.Text.RegularExpressions.Regex.Match(lessonNumberText, @"(\d+)");
                            if (match.Success)
                            {
                                lessonNumber = int.Parse(match.Groups[1].Value);
                            }
                            else
                            {
                                lessonNumber = 0;
                            }
                        }
                        
                        // Получаем предмет
                        string subjectText = "";
                        if (columnIndices.TryGetValue("subject", out int subjectIndex) && 
                            subjectIndex >= 0 && cells.Count > subjectIndex)
                        {
                            subjectText = cells[subjectIndex].InnerText.Trim();
                        }
                        
                        // Пропускаем строки без предмета
                        if (string.IsNullOrEmpty(subjectText))
                        {
                            continue;
                        }
                        
                        // Получаем аудиторию
                        string classroomText = "Не указан";
                        if (columnIndices.TryGetValue("classroom", out int classroomIndex) && 
                            classroomIndex >= 0 && cells.Count > classroomIndex)
                        {
                            var text = cells[classroomIndex].InnerText.Trim();
                            if (!string.IsNullOrEmpty(text))
                            {
                                classroomText = text;
                            }
                        }
                        
                        // Получаем подгруппу, если есть
                        string subgroupText = "0";
                        if (columnIndices.TryGetValue("subgroup", out int subgroupIndex) && 
                            subgroupIndex >= 0 && cells.Count > subgroupIndex)
                        {
                            var text = cells[subgroupIndex].InnerText.Trim();
                            if (!string.IsNullOrEmpty(text))
                            {
                                subgroupText = text;
                            }
                        }
                        
                        // Определяем время начала и конца пары на основе номера
                        TimeSpan startTime, endTime;
                        switch (lessonNumber)
                        {
                            case 1: startTime = new TimeSpan(8, 30, 0); endTime = new TimeSpan(10, 0, 0); break;
                            case 2: startTime = new TimeSpan(10, 10, 0); endTime = new TimeSpan(11, 40, 0); break;
                            case 3: startTime = new TimeSpan(12, 20, 0); endTime = new TimeSpan(13, 50, 0); break;
                            case 4: startTime = new TimeSpan(14, 0, 0); endTime = new TimeSpan(15, 30, 0); break;
                            case 5: startTime = new TimeSpan(15, 40, 0); endTime = new TimeSpan(17, 10, 0); break;
                            case 6: startTime = new TimeSpan(17, 20, 0); endTime = new TimeSpan(18, 50, 0); break;
                            case 7: startTime = new TimeSpan(19, 0, 0); endTime = new TimeSpan(20, 30, 0); break;
                            case 8: startTime = new TimeSpan(20, 40, 0); endTime = new TimeSpan(22, 10, 0); break;
                            default: startTime = new TimeSpan(8, 0, 0); endTime = new TimeSpan(9, 30, 0); break;
                        }
                        
                        // Заполняем группу
                        Models.Group group;
                        if (!string.IsNullOrEmpty(groupText))
                        {
                            if (!groups.TryGetValue(groupText, out group))
                            {
                                group = new Models.Group { Name = groupText };
                                groups[groupText] = group;
                            }
                        }
                        else
                        {
                            // Если группа не указана, создаем условную группу по имени преподавателя
                            var pseudoGroupName = $"Группа для {teacherText}";
                            if (!groups.TryGetValue(pseudoGroupName, out group))
                            {
                                group = new Models.Group { Name = pseudoGroupName };
                                groups[pseudoGroupName] = group;
                            }
                        }
                        
                        // Заполняем предмет
                        if (!subjects.TryGetValue(subjectText, out Subject subject))
                        {
                            subject = new Subject { Name = subjectText };
                            subjects[subjectText] = subject;
                        }
                        
                        // Заполняем преподавателя
                        Teacher teacher;
                        if (!string.IsNullOrEmpty(teacherText))
                        {
                            if (!teachers.TryGetValue(teacherText, out teacher))
                            {
                                teacher = new Teacher { Name = teacherText };
                                teachers[teacherText] = teacher;
                            }
                        }
                        else
                        {
                            // Если преподаватель не указан
                            teacherText = "Не указан";
                            if (!teachers.TryGetValue(teacherText, out teacher))
                            {
                                teacher = new Teacher { Name = teacherText };
                                teachers[teacherText] = teacher;
                            }
                        }
                        
                        // Заполняем аудиторию
                        if (!classrooms.TryGetValue(classroomText, out Classroom classroom))
                        {
                            classroom = new Classroom { Number = classroomText };
                            classrooms[classroomText] = classroom;
                        }
                        
                        // Определяем тип занятия
                        string lessonType = "Лекция";
                        if (int.TryParse(subgroupText, out int subgroup) && subgroup > 0)
                        {
                            lessonType = $"Подгруппа {subgroup}";
                        }
                        else if (subjectText.ToLower().Contains("лаб"))
                        {
                            lessonType = "Лабораторная";
                        }
                        else if (subjectText.ToLower().Contains("практ"))
                        {
                            lessonType = "Практика";
                        }
                        
                        // Создание записи расписания
                        var schedule = new Schedule
                        {
                            Date = parsedDate,
                            StartTime = startTime,
                            EndTime = endTime,
                            Group = group,
                            Subject = subject,
                            Teacher = teacher,
                            Classroom = classroom,
                            LessonType = lessonType
                        };
                        
                        schedules.Add(schedule);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Ошибка при обработке строки {i}: {ex.Message}", LogLevel.Warning);
                    }
                }
                
                Logger.Log($"Всего обработано записей расписания: {schedules.Count}", LogLevel.Info);
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Парсинг HTML страницы");
            }
            
            return schedules;
        }
        
        /// <summary>
        /// Находит лучшую таблицу для расписания среди найденных на странице
        /// </summary>
        private HtmlNode FindBestTable(HtmlNodeCollection tables)
        {
            if (tables == null || tables.Count == 0)
                return null;
                
            // Если есть только одна таблица, возвращаем её
            if (tables.Count == 1)
            {
                Logger.Log("На странице найдена только одна таблица, используем ее", LogLevel.Info);
                return tables[0];
            }
            
            Logger.Log($"На странице найдено {tables.Count} таблиц, ищем подходящую для расписания", LogLevel.Info);
            
            // Ищем таблицу с наибольшим количеством строк и нужным содержимым
            HtmlNode bestTable = null;
            int maxRows = 0;
            int bestScore = 0;
            
            // Сначала проверяем таблицы внутри div с классом item-page (типичный контейнер контента на сайте БТК)
            var contentDiv = tables[0].OwnerDocument.DocumentNode.SelectSingleNode("//div[@class='item-page']");
            if (contentDiv != null)
            {
                var contentTables = contentDiv.SelectNodes(".//table");
                if (contentTables != null && contentTables.Count > 0)
                {
                    Logger.Log($"Найдено {contentTables.Count} таблиц внутри div.item-page", LogLevel.Info);
                    
                    // Если внутри контентного блока только одна таблица, скорее всего это расписание
                    if (contentTables.Count == 1)
                    {
                        Logger.Log("В основном контентном блоке одна таблица, вероятно это расписание", LogLevel.Info);
                        return contentTables[0];
                    }
                    
                    // Если таблиц несколько, заменяем общий список таблиц на таблицы из контентного блока
                    tables = contentTables;
                }
            }
            
            // Ищем таблицу с заголовком, содержащим ключевые слова
            HtmlNode tableWithHeaderText = null;
            string[] headerKeywords = new[] { "расписание", "расписани", "занятия", "пары", "уроки" };
            
            foreach (var table in tables)
            {
                var previousNode = table.PreviousSibling;
                while (previousNode != null && string.IsNullOrWhiteSpace(previousNode.InnerText) && previousNode.NodeType != HtmlNodeType.Element)
                {
                    previousNode = previousNode.PreviousSibling;
                }
                
                if (previousNode != null && (previousNode.Name == "h1" || previousNode.Name == "h2" || 
                    previousNode.Name == "h3" || previousNode.Name == "h4" || previousNode.Name == "h5" || 
                    previousNode.Name == "div" || previousNode.Name == "p" || previousNode.Name == "strong"))
                {
                    string headerText = previousNode.InnerText.ToLower();
                    Logger.Log($"Перед таблицей найден заголовок: '{headerText}'", LogLevel.Debug);
                    
                    if (headerKeywords.Any(keyword => headerText.Contains(keyword)))
                    {
                        Logger.Log("Найдена таблица с подходящим заголовком перед ней", LogLevel.Info);
                        tableWithHeaderText = table;
                        break;
                    }
                }
            }
            
            // Если нашли таблицу с подходящим заголовком, используем её
            if (tableWithHeaderText != null)
            {
                return tableWithHeaderText;
            }
            
            // Если не нашли по заголовку, оцениваем таблицы по содержимому и структуре
            foreach (var table in tables)
            {
                var rows = table.SelectNodes(".//tr");
                if (rows == null) continue;
                
                int rowCount = rows.Count;
                int score = 0;
                
                // Таблица должна иметь достаточное количество строк для расписания
                if (rowCount < 3) continue; // Должен быть хотя бы заголовок и пара строк с данными
                
                // Больше строк - выше вероятность, что это расписание
                score += Math.Min(rowCount, 20); // Начисляем до 20 баллов за количество строк
                
                // Проверяем содержимое первой строки таблицы (заголовок)
                var headerRow = rows[0];
                var headerCells = headerRow.SelectNodes(".//th") ?? headerRow.SelectNodes(".//td");
                
                if (headerCells != null)
                {
                    int headerCellCount = headerCells.Count;
                    
                    // Таблица расписания должна иметь несколько колонок
                    if (headerCellCount >= 4)
                    {
                        score += 5;
                        
                        // Проверяем заголовки на наличие ключевых слов
                        foreach (var cell in headerCells)
                        {
                            string cellText = cell.InnerText.ToLower();
                            
                            if (cellText.Contains("группа") || cellText.Contains("класс"))
                                score += 10;
                            if (cellText.Contains("предмет") || cellText.Contains("дисциплина"))
                                score += 10;
                            if (cellText.Contains("преподаватель") || cellText.Contains("препод"))
                                score += 10;
                            if (cellText.Contains("аудитория") || cellText.Contains("кабинет") || cellText.Contains("каб"))
                                score += 10;
                            if (cellText.Contains("пара") || cellText.Contains("урок") || cellText.Contains("время"))
                                score += 10;
                            if (cellText.Contains("дата") || cellText.Contains("день"))
                                score += 5;
                        }
                    }
                }
                
                // Проверяем наличие ячеек с числовыми значениями (номера пар, аудиторий)
                var allCells = table.SelectNodes(".//td");
                if (allCells != null)
                {
                    int numericCells = 0;
                    foreach (var cell in allCells)
                    {
                        string cellText = cell.InnerText.Trim();
                        if (Regex.IsMatch(cellText, @"^\d+$") || Regex.IsMatch(cellText, @"^\d+\D+$"))
                        {
                            numericCells++;
                        }
                    }
                    
                    // Если есть числовые ячейки, это хороший признак расписания (номера пар, кабинеты)
                    if (numericCells > 0)
                    {
                        score += Math.Min(numericCells, 10);
                    }
                }
                
                // Проверяем общий текст таблицы на ключевые слова
                var tableText = table.InnerText.ToLower();
                foreach (var keyword in headerKeywords)
                {
                    if (tableText.Contains(keyword))
                        score += 5;
                }
                
                // Если нашли таблицу с большим количеством строк или лучшей оценкой
                if (rows.Count > maxRows || score > bestScore)
                {
                    maxRows = rows.Count;
                    bestScore = score;
                    bestTable = table;
                    
                    Logger.Log($"Найдена потенциальная таблица расписания: строк={rows.Count}, оценка={score}", LogLevel.Debug);
                }
            }
            
            if (bestTable != null)
            {
                Logger.Log($"Выбрана наилучшая таблица: строк={maxRows}, оценка={bestScore}", LogLevel.Info);
            }
            else
            {
                Logger.Log("Не удалось найти подходящую таблицу для расписания", LogLevel.Warning);
            }
            
            return bestTable;
        }
        
        /// <summary>
        /// Определяет индексы колонок на основе заголовка таблицы
        /// </summary>
        private Dictionary<string, int> DetectTableColumns(HtmlNode headerRow)
        {
            var result = new Dictionary<string, int>();
            
            if (headerRow == null)
                return result;
                
            // Получаем ячейки заголовка
            var headerCells = headerRow.SelectNodes(".//th");
            if (headerCells == null) headerCells = headerRow.SelectNodes(".//td");
            if (headerCells == null) return result;
            
            Logger.Log($"Анализ заголовка таблицы, найдено ячеек: {headerCells.Count}", LogLevel.Debug);
            
            // Заголовки на русском с учетом возможных вариаций
            var columnMappings = new Dictionary<string, List<string>>
            {
                { "date", new List<string> { "дата", "день", "дата занятия", "день недели" } },
                { "group", new List<string> { "группа", "класс", "группы", "классы", "номер группы" } },
                { "lesson", new List<string> { "пара", "урок", "номер", "номер пары", "номер урока", "время", "время занятия", "№", "№ пары" } },
                { "subject", new List<string> { "предмет", "дисциплина", "название", "занятие", "наименование предмета" } },
                { "teacher", new List<string> { "преподаватель", "препод", "фио", "фио преподавателя", "учитель" } },
                { "classroom", new List<string> { "аудитория", "каб", "кабинет", "ауд", "место", "помещение", "номер аудитории" } },
                { "subgroup", new List<string> { "подгруппа", "подгр", "группировка" } }
            };
            
            // Ищем ключевые слова в заголовках
            for (int i = 0; i < headerCells.Count; i++)
            {
                var cellText = headerCells[i].InnerText.Trim().ToLower();
                Logger.Log($"Заголовок колонки {i}: '{cellText}'", LogLevel.Debug);
                
                // Ищем соответствие во всех словарях
                foreach (var mapping in columnMappings)
                {
                    string columnType = mapping.Key;
                    List<string> keywords = mapping.Value;
                    
                    // Проверяем точное соответствие
                    if (keywords.Contains(cellText))
                    {
                        result[columnType] = i;
                        Logger.Log($"Найдено точное соответствие для колонки '{columnType}' в позиции {i}: '{cellText}'", LogLevel.Debug);
                        break;
                    }
                    
                    // Проверяем вхождение ключевых слов
                    foreach (var keyword in keywords)
                    {
                        if (cellText.Contains(keyword))
                        {
                            result[columnType] = i;
                            Logger.Log($"Найдено соответствие для колонки '{columnType}' в позиции {i}: '{cellText}' содержит '{keyword}'", LogLevel.Debug);
                            break;
                        }
                    }
                }
            }
            
            // Если заголовки не найдены, пробуем определить по содержимому первых ячеек
            if (result.Count == 0)
            {
                Logger.Log("Не удалось определить колонки по заголовкам, анализируем содержимое ячеек", LogLevel.Warning);
                
                // Если первая колонка имеет числа от 1 до 10, вероятно это номера уроков
                var firstCell = headerCells[0].InnerText.Trim();
                if (int.TryParse(firstCell, out int num) && num >= 1 && num <= 10)
                {
                    result["lesson"] = 0;
                    Logger.Log("Определена колонка 'lesson' на позиции 0 на основе цифры в первой ячейке", LogLevel.Debug);
                }
                
                // Если последняя колонка похожа на номер кабинета (содержит цифры и возможно буквы)
                var lastCell = headerCells[headerCells.Count - 1].InnerText.Trim();
                if (Regex.IsMatch(lastCell, @"\d+.*"))
                {
                    result["classroom"] = headerCells.Count - 1;
                    Logger.Log($"Определена колонка 'classroom' на позиции {headerCells.Count - 1} на основе формата номера аудитории", LogLevel.Debug);
                }
            }
            
            // Логируем найденные колонки
            if (result.Count > 0)
            {
                Logger.Log($"Определены следующие колонки: {string.Join(", ", result.Select(kv => $"{kv.Key}={kv.Value}"))}", LogLevel.Info);
            }
            else
            {
                Logger.Log("Не удалось определить ни одной колонки по заголовкам", LogLevel.Warning);
            }
            
            return result;
        }
        
        /// <summary>
        /// Определяет структуру таблицы на основе анализа данных строк
        /// </summary>
        private Dictionary<string, int> InferTableStructure(List<HtmlNode> dataRows)
        {
            var result = new Dictionary<string, int>();
            
            if (dataRows == null || dataRows.Count == 0)
                return result;
                
            // Предположим, что таблица может иметь следующую структуру
            // Колонка 0: Группа или Дата (если таблица для преподавателя)
            // Колонка 1: Номер пары или Группа (если первая колонка - дата)
            // Колонка 2: Предмет или Номер пары (если вторая колонка - группа)
            // Колонка 3: Преподаватель или Предмет (продолжаем сдвиг)
            // Колонка 4: Аудитория или Преподаватель
            // Колонка 5: Подгруппа или Аудитория
            
            // Проверим содержимое первых нескольких строк
            var patternResults = new Dictionary<int, Dictionary<string, int>>();
            
            // Рассмотрим несколько вариантов шаблонов
            
            // Шаблон 1: Группа-Пара-Предмет-Преподаватель-Аудитория
            patternResults[1] = new Dictionary<string, int> {
                { "group", 0 }, { "lesson", 1 }, { "subject", 2 }, 
                { "teacher", 3 }, { "classroom", 4 }
            };
            
            // Шаблон 2: Дата-Группа-Пара-Предмет-Преподаватель-Аудитория
            patternResults[2] = new Dictionary<string, int> {
                { "date", 0 }, { "group", 1 }, { "lesson", 2 },
                { "subject", 3 }, { "teacher", 4 }, { "classroom", 5 }
            };
            
            // Шаблон 3: Пара-Предмет-Преподаватель-Аудитория
            patternResults[3] = new Dictionary<string, int> {
                { "lesson", 0 }, { "subject", 1 }, 
                { "teacher", 2 }, { "classroom", 3 }
            };
            
            // Шаблон 4: Дата-Преподаватель-Пара-Предмет-Группа-Аудитория
            patternResults[4] = new Dictionary<string, int> {
                { "date", 0 }, { "teacher", 1 }, { "lesson", 2 },
                { "subject", 3 }, { "group", 4 }, { "classroom", 5 }
            };
            
            // Оцениваем каждый шаблон
            var patternScores = new Dictionary<int, int>();
            
            foreach (var row in dataRows)
            {
                var cells = row.SelectNodes(".//td");
                if (cells == null || cells.Count < 3) continue;
                
                foreach (var pattern in patternResults)
                {
                    int patternId = pattern.Key;
                    var columns = pattern.Value;
                    
                    // Оцениваем соответствие шаблону
                    int score = 0;
                    
                    // Проверяем каждую колонку на соответствие ожидаемому типу данных
                    foreach (var col in columns)
                    {
                        string type = col.Key;
                        int index = col.Value;
                        
                        if (index < 0 || index >= cells.Count) continue;
                        
                        string cellText = cells[index].InnerText.Trim();
                        if (string.IsNullOrWhiteSpace(cellText)) continue;
                        
                        if (type == "date" && TryParseDate(cellText, out _))
                            score += 3;
                        else if (type == "group" && Regex.IsMatch(cellText, @"\d+\w*"))
                            score += 2;
                        else if (type == "lesson" && Regex.IsMatch(cellText, @"^\d+"))
                            score += 2;
                        else if (type == "subject" && cellText.Length > 5)
                            score += 1;
                        else if (type == "teacher" && cellText.Contains(" "))
                            score += 2;
                        else if (type == "classroom" && (Regex.IsMatch(cellText, @"\d+") || 
                                 cellText.ToUpper().Contains("АУД")))
                            score += 2;
                    }
                    
                    // Добавляем очки к общей оценке шаблона
                    if (!patternScores.ContainsKey(patternId))
                        patternScores[patternId] = 0;
                        
                    patternScores[patternId] += score;
                }
            }
            
            // Выбираем шаблон с наивысшей оценкой
            int bestPattern = 0;
            int maxScore = 0;
            
            foreach (var score in patternScores)
            {
                if (score.Value > maxScore)
                {
                    maxScore = score.Value;
                    bestPattern = score.Key;
                }
            }
            
            if (bestPattern > 0)
            {
                Logger.Log($"Определен наилучший шаблон для таблицы: {bestPattern} (оценка: {maxScore})", LogLevel.Info);
                return patternResults[bestPattern];
            }
            
            // Если не удалось определить, предполагаем стандартную структуру
            return new Dictionary<string, int> {
                { "group", 0 }, { "lesson", 1 }, { "subject", 2 }, 
                { "teacher", 3 }, { "classroom", 4 }
            };
        }
        
        /// <summary>
        /// Попытка парсинга даты из строки
        /// </summary>
        private bool TryParseDate(string dateText, out DateTime date)
        {
            date = DateTime.Today; // По умолчанию
            
            if (string.IsNullOrWhiteSpace(dateText))
                return false;
            
            // Словарь месяцев на русском
            var monthDict = new Dictionary<string, int>
            {
                {"янв", 1}, {"фев", 2}, {"март", 3}, {"мар", 3}, {"апр", 4},
                {"май", 5}, {"мая", 5}, {"июн", 6}, {"июл", 7}, 
                {"авг", 8}, {"сент", 9}, {"сен", 9}, {"окт", 10}, {"ноя", 11}, 
                {"нояб", 11}, {"дек", 12}
            };
            
            try
            {
                // Очищаем строку
                dateText = dateText.Trim().ToLower();
                
                // Удаляем лишние символы вокруг даты
                dateText = dateText.Trim('(', ')', '[', ']', '{', '}', '«', '»', '"', ',', ':', '.', '!', '?');
                
                Logger.Log($"Парсинг даты: {dateText}", LogLevel.Debug);
                
                // Проверяем форматы из списка "22 мая", "22-мая", "22 мая 2023"
                foreach (var separator in new[] { ' ', '-', '.' })
                {
                    var parts = dateText.Split(separator);
                    if (parts.Length >= 2)
                    {
                        // Находим день (первая часть должна быть числом)
                        if (!int.TryParse(parts[0], out int day))
                            continue;
                        
                        // Находим месяц (вторая часть должна быть месяцем)
                        var monthStr = parts[1].ToLower();
                        int month = 0;
                        
                        foreach (var kvp in monthDict)
                        {
                            if (monthStr.StartsWith(kvp.Key))
                            {
                                month = kvp.Value;
                                break;
                            }
                        }
                        
                        if (month > 0)
                        {
                            // Определяем год (берем из строки или текущий)
                            int year = DateTime.Now.Year;
                            if (parts.Length >= 3 && int.TryParse(parts[2], out int parsedYear))
                            {
                                year = parsedYear < 100 ? 2000 + parsedYear : parsedYear;
                            }
                            else if (month < DateTime.Now.Month)
                            {
                                // Если месяц уже прошел, значит это скорее всего следующий год
                                year++;
                            }
                            
                            // Проверяем, что дата валидна
                            if (day >= 1 && day <= DateTime.DaysInMonth(year, month))
                            {
                                date = new DateTime(year, month, day);
                                Logger.Log($"Успешный парсинг даты: {date.ToShortDateString()}", LogLevel.Info);
                                return true;
                            }
                        }
                    }
                }
                
                // Проверяем формат с названием дня недели "Понедельник, 22 мая"
                var dayNamesRu = new[] { "понедельник", "вторник", "среда", "четверг", "пятница", "суббота", "воскресенье" };
                foreach (var dayName in dayNamesRu)
                {
                    if (dateText.Contains(dayName))
                    {
                        var index = dateText.IndexOf(dayName) + dayName.Length;
                        if (index < dateText.Length)
                        {
                            var restText = dateText.Substring(index).Trim(' ', ',', ':', '.', '-');
                            if (TryParseDate(restText, out date))
                                return true;
                        }
                    }
                }
                
                // Проверяем формат "дд.мм.гггг"
                if (DateTime.TryParseExact(dateText, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out date))
                {
                    Logger.Log($"Успешный парсинг даты через TryParseExact: {date.ToShortDateString()}", LogLevel.Info);
                    return true;
                }
                
                // Пробуем стандартные форматы
                string[] formats = { "dd.MM.yyyy", "yyyy-MM-dd", "dd/MM/yyyy", "MM/dd/yyyy", "dd.MM.yy" };
                if (DateTime.TryParseExact(dateText, formats, System.Globalization.CultureInfo.InvariantCulture, 
                                         System.Globalization.DateTimeStyles.None, out date))
                {
                    Logger.Log($"Успешный парсинг даты через TryParseExact: {date.ToShortDateString()}", LogLevel.Info);
                    return true;
                }
                
                // Если ничего не помогло, пробуем общий метод Parse
                if (DateTime.TryParse(dateText, out date))
                {
                    Logger.Log($"Успешный парсинг даты через TryParse: {date.ToShortDateString()}", LogLevel.Info);
                    return true;
                }
                
                Logger.Log($"Не удалось распознать дату: {dateText}", LogLevel.Warning);
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Парсинг даты");
                return false;
            }
        }
        
        /// <summary>
        /// Выполняет тестовую проверку доступности сайта БТК без обработки данных
        /// </summary>
        /// <returns>Строку с результатами проверки</returns>
        public async Task<string> TestConnectionAsync()
        {
            try
            {
                Logger.Log("Начало тестирования подключения к сайту БТК", LogLevel.Info);
                var result = new System.Text.StringBuilder();
                result.AppendLine("Результаты проверки подключения к сайту БТК:");
                
                // Шаг 1: Проверка подключения к интернету через Google
                try 
                {
                    var googleClient = new HttpClient();
                    googleClient.Timeout = TimeSpan.FromSeconds(5);
                    var googleResponse = await googleClient.GetAsync("https://www.google.com");
                    result.AppendLine($"1. Проверка интернета: УСПЕШНО (Код: {googleResponse.StatusCode})");
                    Logger.Log($"Проверка подключения к интернету: Успешно (Код: {googleResponse.StatusCode})", LogLevel.Info);
                }
                catch (Exception ex)
                {
                    result.AppendLine($"1. Проверка интернета: ОШИБКА ({ex.GetType().Name}: {ex.Message})");
                    result.AppendLine("   Рекомендация: Проверьте подключение к интернету");
                    Logger.LogException(ex, "Проверка подключения к интернету");
                    return result.ToString();
                }
                
                // Шаг 2: Выполнение HEAD-запроса к сайту БТК
                try
                {
                    var headClient = new HttpClient();
                    headClient.Timeout = TimeSpan.FromSeconds(10);
                    var request = new HttpRequestMessage(HttpMethod.Head, _defaultUrl);
                    var headResponse = await headClient.SendAsync(request);
                    result.AppendLine($"2. Проверка доступности сайта БТК: УСПЕШНО (Код: {headResponse.StatusCode})");
                    Logger.Log($"Проверка доступности сайта БТК: Успешно (Код: {headResponse.StatusCode})", LogLevel.Info);
                }
                catch (Exception ex)
                {
                    result.AppendLine($"2. Проверка доступности сайта БТК: ОШИБКА ({ex.GetType().Name}: {ex.Message})");
                    result.AppendLine("   Рекомендация: Возможно, сайт БТК недоступен или заблокирован");
                    Logger.LogException(ex, "Проверка доступности сайта БТК");
                    return result.ToString();
                }
                
                // Шаг 3: Попытка загрузить страницу с расписанием
                try
                {
                    var pageClient = new HttpClient();
                    pageClient.Timeout = TimeSpan.FromSeconds(20);
                    pageClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
                    var html = await pageClient.GetStringAsync(_defaultUrl);
                    
                    result.AppendLine($"3. Загрузка страницы расписания: УСПЕШНО (Длина HTML: {html.Length} символов)");
                    Logger.Log($"Загрузка страницы расписания: Успешно (Длина HTML: {html.Length} символов)", LogLevel.Info);
                    
                    // Шаг 4: Проверка наличия таблиц и ключевых слов
                    if (html.Contains("table"))
                    {
                        result.AppendLine("4. Проверка наличия таблиц: УСПЕШНО");
                        Logger.Log("Проверка наличия таблиц: Успешно", LogLevel.Info);
                    }
                    else
                    {
                        result.AppendLine("4. Проверка наличия таблиц: ОШИБКА (Таблицы не найдены)");
                        result.AppendLine("   Рекомендация: Возможно, структура сайта изменилась");
                        Logger.Log("Проверка наличия таблиц: Ошибка - таблицы не найдены", LogLevel.Warning);
                    }
                    
                    if (html.Contains("расписание", StringComparison.OrdinalIgnoreCase))
                    {
                        result.AppendLine("5. Проверка ключевых слов: УСПЕШНО");
                        Logger.Log("Проверка ключевых слов: Успешно", LogLevel.Info);
                    }
                    else
                    {
                        result.AppendLine("5. Проверка ключевых слов: ОШИБКА (Ключевое слово 'расписание' не найдено)");
                        result.AppendLine("   Рекомендация: Возможно, вы попали на другую страницу сайта");
                        Logger.Log("Проверка ключевых слов: Ошибка - ключевое слово 'расписание' не найдено", LogLevel.Warning);
                    }
                    
                    // Шаг 5: Проверка парсера
                    try
                    {
                        var htmlDoc = new HtmlDocument();
                        htmlDoc.LoadHtml(html);
                        
                        var tables = htmlDoc.DocumentNode.SelectNodes("//div[@class='item-page']//table") ?? 
                                    htmlDoc.DocumentNode.SelectNodes("//table");
                        
                        if (tables != null && tables.Any())
                        {
                            result.AppendLine($"6. Проверка парсера таблиц: УСПЕШНО (Найдено таблиц: {tables.Count})");
                            Logger.Log($"Проверка парсера таблиц: Успешно (Найдено таблиц: {tables.Count})", LogLevel.Info);
                        }
                        else
                        {
                            result.AppendLine("6. Проверка парсера таблиц: ОШИБКА (Не удалось найти таблицы через парсер)");
                            Logger.Log("Проверка парсера таблиц: Ошибка - не удалось найти таблицы через парсер", LogLevel.Warning);
                        }
                    }
                    catch (Exception ex)
                    {
                        result.AppendLine($"6. Проверка парсера таблиц: ОШИБКА ({ex.GetType().Name}: {ex.Message})");
                        Logger.LogException(ex, "Проверка парсера таблиц");
                    }
                }
                catch (Exception ex)
                {
                    result.AppendLine($"3. Загрузка страницы расписания: ОШИБКА ({ex.GetType().Name}: {ex.Message})");
                    result.AppendLine("   Рекомендация: Проверьте доступность сайта БТК через браузер");
                    Logger.LogException(ex, "Загрузка страницы расписания");
                }
                
                Logger.Log("Тестирование подключения к сайту БТК завершено", LogLevel.Info);
                return result.ToString();
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Тестирование подключения к сайту БТК");
                return $"Ошибка при выполнении теста подключения: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Метод для обновления расписания в базе данных из внешнего источника
        /// </summary>
        public async Task<bool> UpdateScheduleFromRemoteSourceAsync(ScheduleViewer.Data.ScheduleDbContext dbContext, string url = null)
        {
            try
            {
                // Если URL не указан, используем URL по умолчанию
                url = string.IsNullOrWhiteSpace(url) ? _defaultUrl : url;
                Logger.Log($"Обновление расписания из URL: {url}", LogLevel.Info);
                
                var schedulesFromSite = await ParseScheduleAsync(url);
                
                if (schedulesFromSite == null || !schedulesFromSite.Any())
                {
                    Logger.Log("Не удалось получить расписание с сайта или расписание пусто", LogLevel.Warning);
                    return false;
                }
                
                Logger.Log($"Получено {schedulesFromSite.Count} записей расписания", LogLevel.Info);
                
                // Словари для хранения уникальных элементов
                var uniqueGroups = new Dictionary<string, Models.Group>();
                var uniqueSubjects = new Dictionary<string, Models.Subject>();
                var uniqueTeachers = new Dictionary<string, Models.Teacher>();
                var uniqueClassrooms = new Dictionary<string, Models.Classroom>();
                
                // Формируем списки уникальных сущностей
                foreach (var schedule in schedulesFromSite)
                {
                    try
                    {
                        // Группы
                        if (!string.IsNullOrWhiteSpace(schedule.Group.Name) && !uniqueGroups.ContainsKey(schedule.Group.Name))
                        {
                            uniqueGroups[schedule.Group.Name] = schedule.Group;
                        }
                        
                        // Предметы
                        if (!string.IsNullOrWhiteSpace(schedule.Subject.Name) && !uniqueSubjects.ContainsKey(schedule.Subject.Name))
                        {
                            uniqueSubjects[schedule.Subject.Name] = schedule.Subject;
                        }
                        
                        // Преподаватели
                        string teacherKey = schedule.Teacher.Name;
                        if (!string.IsNullOrWhiteSpace(teacherKey) && !uniqueTeachers.ContainsKey(teacherKey))
                        {
                            uniqueTeachers[teacherKey] = schedule.Teacher;
                        }
                        
                        // Аудитории
                        if (!string.IsNullOrWhiteSpace(schedule.Classroom.Number) && !uniqueClassrooms.ContainsKey(schedule.Classroom.Number))
                        {
                            uniqueClassrooms[schedule.Classroom.Number] = schedule.Classroom;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Ошибка при обработке записи расписания: {ex.Message}", LogLevel.Error);
                    }
                }
                
                Logger.Log($"Уникальных групп: {uniqueGroups.Count}", LogLevel.Info);
                
                // Сохраняем группы в базу данных
                foreach (var groupEntry in uniqueGroups)
                {
                    // Проверяем, существует ли группа с таким именем
                    var existingGroup = await dbContext.Groups.FirstOrDefaultAsync(g => g.Name == groupEntry.Key);
                    if (existingGroup == null)
                    {
                        dbContext.Groups.Add(groupEntry.Value);
                    }
                }
                
                Logger.Log($"Уникальных предметов: {uniqueSubjects.Count}", LogLevel.Info);
                
                // Сохраняем предметы в базу данных
                foreach (var subjectEntry in uniqueSubjects)
                {
                    // Проверяем, существует ли предмет с таким именем
                    var existingSubject = await dbContext.Subjects.FirstOrDefaultAsync(s => s.Name == subjectEntry.Key);
                    if (existingSubject == null)
                    {
                        dbContext.Subjects.Add(subjectEntry.Value);
                    }
                }
                
                Logger.Log($"Уникальных преподавателей: {uniqueTeachers.Count}", LogLevel.Info);
                
                // Сохраняем преподавателей в базу данных
                foreach (var teacherEntry in uniqueTeachers)
                {
                    var teacher = teacherEntry.Value;
                    // Проверяем, существует ли преподаватель с таким именем
                    var existingTeacher = await dbContext.Teachers.FirstOrDefaultAsync(t => t.Name == teacher.Name);
                            
                    if (existingTeacher == null)
                    {
                        dbContext.Teachers.Add(teacher);
                    }
                }
                
                Logger.Log($"Уникальных аудиторий: {uniqueClassrooms.Count}", LogLevel.Info);
                
                // Сохраняем аудитории в базу данных
                foreach (var classroomEntry in uniqueClassrooms)
                {
                    // Проверяем, существует ли аудитория с таким номером
                    var existingClassroom = await dbContext.Classrooms.FirstOrDefaultAsync(c => c.Number == classroomEntry.Key);
                    if (existingClassroom == null)
                    {
                        dbContext.Classrooms.Add(classroomEntry.Value);
                    }
                }
                
                // Сохраняем изменения, чтобы получить ID для сущностей
                await dbContext.SaveChangesAsync();
                
                // Очищаем существующее расписание
                Logger.Log("Очищаем существующее расписание в базе данных", LogLevel.Info);
                dbContext.Schedules.RemoveRange(dbContext.Schedules);
                await dbContext.SaveChangesAsync();
                
                // Словарь для отслеживания уже добавленных расписаний
                var schedulesByKey = new Dictionary<string, Models.Schedule>();
                
                // Создаем расписание с привязкой к существующим сущностям
                foreach (var scheduleFromSite in schedulesFromSite)
                {
                    try 
                    {
                        // Получаем ссылки на сущности в базе данных
                        var group = await dbContext.Groups.FirstOrDefaultAsync(g => g.Name == scheduleFromSite.Group.Name);
                        var subject = await dbContext.Subjects.FirstOrDefaultAsync(s => s.Name == scheduleFromSite.Subject.Name);
                        
                        // Ищем преподавателя
                        var teacher = await dbContext.Teachers.FirstOrDefaultAsync(t => t.Name == scheduleFromSite.Teacher.Name);
                            
                        var classroom = await dbContext.Classrooms.FirstOrDefaultAsync(c => c.Number == scheduleFromSite.Classroom.Number);
                        
                        if (group == null || subject == null || teacher == null || classroom == null)
                        {
                            Logger.Log($"Пропуск записи расписания из-за отсутствия связанных сущностей: Группа={scheduleFromSite.Group.Name}, Предмет={scheduleFromSite.Subject.Name}", LogLevel.Warning);
                            continue;
                        }
                        
                        // Создаем уникальный ключ для предотвращения дублирования
                        string key = $"{scheduleFromSite.Date:yyyy-MM-dd}_{group.Id}_{scheduleFromSite.LessonNumber}_{subject.Id}";
                        
                        if (!schedulesByKey.ContainsKey(key))
                        {
                            var newSchedule = new Models.Schedule
                            {
                                Date = scheduleFromSite.Date,
                                // Не можем напрямую установить LessonNumber, т.к. это read-only свойство
                                // Вместо этого установим StartTime и EndTime
                                StartTime = GetStartTimeForLesson(scheduleFromSite.LessonNumber, scheduleFromSite.Date),
                                EndTime = GetEndTimeForLesson(scheduleFromSite.LessonNumber, scheduleFromSite.Date),
                                LessonType = scheduleFromSite.LessonType,
                                GroupId = group.Id,
                                SubjectId = subject.Id,
                                TeacherId = teacher.Id,
                                ClassroomId = classroom.Id
                            };
                            
                            schedulesByKey[key] = newSchedule;
                            dbContext.Schedules.Add(newSchedule);
                        }
                        else
                        {
                            Logger.Log($"Пропуск дубликата расписания: {key}", LogLevel.Debug);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Ошибка при добавлении записи расписания: {ex.Message}", LogLevel.Error);
                    }
                }
                
                // Сохраняем изменения
                Logger.Log($"Сохраняем {schedulesByKey.Count} записей расписания в базу данных", LogLevel.Info);
                await dbContext.SaveChangesAsync();
                
                Logger.Log("Расписание успешно обновлено", LogLevel.Info);
                
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Ошибка при обновлении расписания: {ex.Message}", LogLevel.Error);
                Logger.Log($"Stack Trace: {ex.StackTrace}", LogLevel.Error);
                
                if (ex.InnerException != null)
                {
                    Logger.Log($"Вложенное исключение: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}", LogLevel.Error);
                    Logger.Log($"Inner Stack Trace: {ex.InnerException.StackTrace}", LogLevel.Error);
                }
                
                return false;
            }
        }
        
        /// <summary>
        /// Получает время начала пары по ее номеру и дню недели
        /// </summary>
        private TimeSpan GetStartTimeForLesson(int lessonNumber, DateTime date)
        {
            string dayType = GetDayType((int)date.DayOfWeek);
            
            switch (lessonNumber)
            {
                case 1: return new TimeSpan(8, 0, 0);
                case 2: return new TimeSpan(9, 50, 0);
                case 3: return new TimeSpan(12, 20, 0);
                case 4:
                    if (dayType == "tuesday") return new TimeSpan(15, 5, 0);
                    return new TimeSpan(14, 10, 0);
                case 5:
                    if (dayType == "tuesday") return new TimeSpan(16, 55, 0);
                    if (dayType == "thursday") return new TimeSpan(16, 35, 0);
                    return new TimeSpan(16, 0, 0);
                case 6:
                    if (dayType == "tuesday") return new TimeSpan(18, 45, 0);
                    if (dayType == "thursday") return new TimeSpan(18, 25, 0);
                    if (dayType == "saturday") return new TimeSpan(17, 5, 0);
                    return new TimeSpan(17, 50, 0);
                default: return new TimeSpan(0, 0, 0);
            }
        }
        
        /// <summary>
        /// Получает время окончания пары по ее номеру и дню недели
        /// </summary>
        private TimeSpan GetEndTimeForLesson(int lessonNumber, DateTime date)
        {
            string dayType = GetDayType((int)date.DayOfWeek);
            
            switch (lessonNumber)
            {
                case 1: return new TimeSpan(9, 40, 0);
                case 2: return new TimeSpan(11, 30, 0);
                case 3: return new TimeSpan(14, 0, 0);
                case 4:
                    if (dayType == "tuesday") return new TimeSpan(16, 45, 0);
                    return new TimeSpan(15, 50, 0);
                case 5:
                    if (dayType == "tuesday") return new TimeSpan(18, 35, 0);
                    if (dayType == "thursday") return new TimeSpan(18, 15, 0);
                    return new TimeSpan(17, 40, 0);
                case 6:
                    if (dayType == "tuesday") return new TimeSpan(20, 20, 0);
                    if (dayType == "thursday") return new TimeSpan(20, 0, 0);
                    if (dayType == "saturday") return new TimeSpan(18, 40, 0);
                    return new TimeSpan(19, 25, 0);
                default: return new TimeSpan(0, 0, 0);
            }
        }
        
        /// <summary>
        /// Получает тип дня недели
        /// </summary>
        private string GetDayType(int weekday)
        {
            switch (weekday)
            {
                case 2: // Tuesday
                    return "tuesday";
                case 4: // Thursday
                    return "thursday";
                case 6: // Saturday
                    return "saturday";
                default:
                    return "normal"; // Monday, Wednesday, Friday
            }
        }
        
        /// <summary>
        /// Проверяет, содержит ли текст русские символы
        /// </summary>
        private bool ContainsRussianText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;
                
            // Диапазоны Unicode для русских букв
            // А-Я: U+0410-U+042F, а-я: U+0430-U+044F, Ё: U+0401, ё: U+0451
            
            // Считаем количество русских символов
            int russianCharsCount = 0;
            
            foreach (char c in text)
            {
                if ((c >= '\u0410' && c <= '\u044F') || c == '\u0401' || c == '\u0451')
                {
                    russianCharsCount++;
                    
                    // Достаточно найти несколько русских символов
                    if (russianCharsCount >= 10)
                        return true;
                }
            }
            
            // Проверяем также ключевые слова на русском в различных кодировках
            // Иногда символы могут быть некорректно декодированы, но все же распознаваемы
            string lowerText = text.ToLowerInvariant();
            
            string[] russianKeywords = {
                "расписание", "группа", "преподаватель", "аудитория", "пара", "урок",
                "занятие", "предмет", "дисциплина", "класс", "день"
            };
            
            foreach (var keyword in russianKeywords)
            {
                if (lowerText.Contains(keyword))
                {
                    return true;
                }
            }
            
            return russianCharsCount > 0;
        }
    }
} 