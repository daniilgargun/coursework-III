using Microsoft.EntityFrameworkCore;
using ScheduleViewer.Models;
using System;
using System.IO;

namespace ScheduleViewer.Data
{
    public class ScheduleDbContext : DbContext
    {
        // DbSet для каждой таблицы базы данных
        public DbSet<Group> Groups { get; set; } = null!;
        public DbSet<Subject> Subjects { get; set; } = null!;
        public DbSet<Teacher> Teachers { get; set; } = null!;
        public DbSet<Classroom> Classrooms { get; set; } = null!;
        public DbSet<Schedule> Schedules { get; set; } = null!;

        // Путь к файлу базы данных
        private static readonly string DbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "BD", "schedule.db");

        public ScheduleDbContext()
        {
            // Создаем директорию для базы данных, если она не существует
            var folder = Path.GetDirectoryName(DbPath);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder!);
                
            // Обеспечиваем создание базы данных
            Database.EnsureCreated();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite($"Data Source={DbPath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Настройка связей между таблицами
            modelBuilder.Entity<Schedule>()
                .HasOne(s => s.Group)
                .WithMany(g => g.Schedules)
                .HasForeignKey(s => s.GroupId);

            modelBuilder.Entity<Schedule>()
                .HasOne(s => s.Subject)
                .WithMany(su => su.Schedules)
                .HasForeignKey(s => s.SubjectId);

            modelBuilder.Entity<Schedule>()
                .HasOne(s => s.Teacher)
                .WithMany(t => t.Schedules)
                .HasForeignKey(s => s.TeacherId);

            modelBuilder.Entity<Schedule>()
                .HasOne(s => s.Classroom)
                .WithMany(c => c.Schedules)
                .HasForeignKey(s => s.ClassroomId);
        }
    }
} 