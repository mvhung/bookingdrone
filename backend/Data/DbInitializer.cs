using EventMap.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EventMap.Api.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(AppDbContext context)
    {
        // Ensure database is created
        await context.Database.EnsureCreatedAsync();

        // Safety SQLite migrations for newly added tables/columns
        try
        {
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""ServiceTypes"" (
                    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_ServiceTypes"" PRIMARY KEY AUTOINCREMENT,
                    ""Code"" TEXT NOT NULL,
                    ""Name"" TEXT NOT NULL,
                    ""Icon"" TEXT NOT NULL,
                    ""Description"" TEXT NULL,
                    ""IsActive"" INTEGER NOT NULL DEFAULT 1,
                    ""DisplayOrder"" INTEGER NOT NULL DEFAULT 0
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_ServiceTypes_Code"" ON ""ServiceTypes"" (""Code"");
            ");
        }
        catch { }

        try
        {
            await context.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE ""Users"" ADD COLUMN ""IsAdmin"" INTEGER NOT NULL DEFAULT 0;
            ");
        }
        catch { }

        try
        {
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""SavedVenues"" (
                    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_SavedVenues"" PRIMARY KEY AUTOINCREMENT,
                    ""Name"" TEXT NOT NULL,
                    ""SubAddress"" TEXT NULL,
                    ""Latitude"" TEXT NOT NULL,
                    ""Longitude"" TEXT NOT NULL,
                    ""UsageCount"" INTEGER NOT NULL DEFAULT 1,
                    ""CreatedAt"" TEXT NOT NULL
                );
            ");
        }
        catch { }

        // User workflow: "Ban đầu thì sẽ không có địa điểm nào cả. Khi tạo job ở địa điểm nào thì mới tự động lưu địa điểm đó."


        // Seed default service catalog if empty
        if (!await context.ServiceTypes.AnyAsync())
        {
            context.ServiceTypes.AddRange(
                new ServiceType { Code = "BUTTERFLY", Name = "Bướm", Icon = "🦋", DisplayOrder = 1 },
                new ServiceType { Code = "FPV", Name = "FPV", Icon = "🚁", DisplayOrder = 2 },
                new ServiceType { Code = "FLYCAM", Name = "Flycam", Icon = "✈️", DisplayOrder = 3 },
                new ServiceType { Code = "CAMERA", Name = "Quay", Icon = "🎥", DisplayOrder = 4 },
                new ServiceType { Code = "PHOTO", Name = "Chụp", Icon = "📸", DisplayOrder = 5 }
            );
            await context.SaveChangesAsync();
        }

        if (await context.Users.AnyAsync())
        {
            // Ensure Hưng is admin if already seeded
            var adminUser = await context.Users.FirstOrDefaultAsync(u => u.Name == "Hưng");
            if (adminUser != null && !adminUser.IsAdmin)
            {
                adminUser.IsAdmin = true;
                await context.SaveChangesAsync();
            }
            return; // DB already seeded
        }

        // Default PIN: 1234 for all users in development / initial rollout
        var defaultPinHash = BCrypt.Net.BCrypt.HashPassword("1234");

        var users = new List<User>
        {
            new User
            {
                Name = "Hưng",
                IsAdmin = true,
                PinHash = defaultPinHash,
                AvatarUrl = "https://api.dicebear.com/7.x/bottts/svg?seed=Hung&backgroundColor=b6e3f4",
                ServicesJson = JsonSerializer.Serialize(new[] { "BUTTERFLY", "FPV", "FLYCAM" }),
                OfficeBusyEnabled = true,
                OfficeBusyStart = new TimeOnly(8, 0),
                OfficeBusyEnd = new TimeOnly(17, 30)
            },
            new User
            {
                Name = "Minh",
                PinHash = defaultPinHash,
                AvatarUrl = "https://api.dicebear.com/7.x/bottts/svg?seed=Minh&backgroundColor=c0aede",
                ServicesJson = JsonSerializer.Serialize(new[] { "BUTTERFLY", "FLYCAM" }),
                OfficeBusyEnabled = false,
                OfficeBusyStart = new TimeOnly(8, 0),
                OfficeBusyEnd = new TimeOnly(17, 30)
            },
            new User
            {
                Name = "Tuấn",
                PinHash = defaultPinHash,
                AvatarUrl = "https://api.dicebear.com/7.x/bottts/svg?seed=Tuan&backgroundColor=d1d4f9",
                ServicesJson = JsonSerializer.Serialize(new[] { "PHOTO" }),
                OfficeBusyEnabled = false,
                OfficeBusyStart = new TimeOnly(8, 0),
                OfficeBusyEnd = new TimeOnly(17, 30)
            },
            new User
            {
                Name = "Hải",
                PinHash = defaultPinHash,
                AvatarUrl = "https://api.dicebear.com/7.x/bottts/svg?seed=Hai&backgroundColor=ffd5dc",
                ServicesJson = JsonSerializer.Serialize(new[] { "CAMERA", "FLYCAM" }),
                OfficeBusyEnabled = false,
                OfficeBusyStart = new TimeOnly(8, 0),
                OfficeBusyEnd = new TimeOnly(17, 30)
            },
            new User
            {
                Name = "Long",
                PinHash = defaultPinHash,
                AvatarUrl = "https://api.dicebear.com/7.x/bottts/svg?seed=Long&backgroundColor=ffdfbf",
                ServicesJson = JsonSerializer.Serialize(new[] { "CAMERA" }),
                OfficeBusyEnabled = true,
                OfficeBusyStart = new TimeOnly(8, 30),
                OfficeBusyEnd = new TimeOnly(17, 30)
            },
            new User
            {
                Name = "Khánh",
                PinHash = defaultPinHash,
                AvatarUrl = "https://api.dicebear.com/7.x/bottts/svg?seed=Khanh&backgroundColor=c0eb75",
                ServicesJson = JsonSerializer.Serialize(new[] { "PHOTO" }),
                OfficeBusyEnabled = false,
                OfficeBusyStart = new TimeOnly(8, 0),
                OfficeBusyEnd = new TimeOnly(17, 30)
            },
            new User
            {
                Name = "Việt",
                PinHash = defaultPinHash,
                AvatarUrl = "https://api.dicebear.com/7.x/bottts/svg?seed=Viet&backgroundColor=a9e34b",
                ServicesJson = JsonSerializer.Serialize(new[] { "BUTTERFLY", "FPV" }),
                OfficeBusyEnabled = false,
                OfficeBusyStart = new TimeOnly(8, 0),
                OfficeBusyEnd = new TimeOnly(17, 30)
            },
            new User
            {
                Name = "Tùng",
                PinHash = defaultPinHash,
                AvatarUrl = "https://api.dicebear.com/7.x/bottts/svg?seed=Tung&backgroundColor=ffe066",
                ServicesJson = JsonSerializer.Serialize(new[] { "CAMERA", "PHOTO" }),
                OfficeBusyEnabled = false,
                OfficeBusyStart = new TimeOnly(8, 0),
                OfficeBusyEnd = new TimeOnly(17, 30)
            }
        };

        await context.Users.AddRangeAsync(users);
        await context.SaveChangesAsync();

        // Seed Busy Dates (e.g. Việt takes personal day off on 2026-10-18)
        var viet = users.First(u => u.Name == "Việt");
        var busyDates = new List<UserBusyDate>
        {
            new UserBusyDate
            {
                UserId = viet.Id,
                BusyDate = new DateOnly(2026, 10, 18),
                Reason = "Báo nghỉ việc gia đình"
            }
        };
        await context.UserBusyDates.AddRangeAsync(busyDates);

        // Seed Sample Jobs for October 2026
        var hung = users.First(u => u.Name == "Hưng");
        var minh = users.First(u => u.Name == "Minh");
        var tuan = users.First(u => u.Name == "Tuấn");
        var hai = users.First(u => u.Name == "Hải");

        var jobs = new List<EventJob>
        {
            new EventJob
            {
                CreatedByUserId = hai.Id,
                OwnerUserId = hai.Id,
                EventDate = new DateOnly(2026, 10, 18),
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(11, 30),
                ServiceCode = "BUTTERFLY",
                LocationName = "Trống Đồng Palace Cảnh Hồ",
                SubAddress = "173B Trường Chinh, Thanh Xuân, Hà Nội",
                Latitude = 20.998200m,
                Longitude = 105.826500m,
                ClientTotalRevenue = 6000000m,
                ExternalWorkersNote = "",
                Note = "2 Bướm trắng sảnh tầng 2, kiểm tra cánh trước 30p",
                Assignments = new List<JobAssignment>
                {
                    new JobAssignment { UserId = hung.Id, CastAmount = 2000000m, IsPaid = false, PersonalNote = "Bay chính" },
                    new JobAssignment { UserId = minh.Id, CastAmount = 2000000m, IsPaid = true, PersonalNote = "Bay phụ" }
                }
            },
            new EventJob
            {
                CreatedByUserId = hai.Id,
                OwnerUserId = hai.Id,
                EventDate = new DateOnly(2026, 10, 18),
                StartTime = new TimeOnly(14, 0),
                EndTime = new TimeOnly(17, 30),
                ServiceCode = "FPV",
                LocationName = "JW Marriott Hotel Hanoi",
                SubAddress = "Số 8 Đỗ Đức Dục, Nam Từ Liêm, Hà Nội",
                Latitude = 21.004400m,
                Longitude = 105.781500m,
                ClientTotalRevenue = null, // Owner will fill in later
                ExternalWorkersNote = "",
                Note = "Bay FPV trao nhẫn cưới",
                Assignments = new List<JobAssignment>
                {
                    new JobAssignment { UserId = hung.Id, CastAmount = 2000000m, IsPaid = true, PersonalNote = "Bay FPV" }
                }
            },
            new EventJob
            {
                CreatedByUserId = tuan.Id,
                OwnerUserId = tuan.Id,
                EventDate = new DateOnly(2026, 10, 18),
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(12, 0),
                ServiceCode = "PHOTO",
                LocationName = "Sheraton West Hanoi",
                SubAddress = "Mỹ Đình, Nam Từ Liêm, Hà Nội",
                Latitude = 21.058500m,
                Longitude = 105.831200m,
                ClientTotalRevenue = 3500000m,
                ExternalWorkersNote = "",
                Note = "Chụp phóng sự cưới",
                Assignments = new List<JobAssignment>
                {
                    new JobAssignment { UserId = tuan.Id, CastAmount = 3500000m, IsPaid = false }
                }
            },
            new EventJob
            {
                CreatedByUserId = hai.Id,
                OwnerUserId = hai.Id,
                EventDate = new DateOnly(2026, 10, 25),
                StartTime = new TimeOnly(17, 30),
                EndTime = new TimeOnly(21, 0),
                ServiceCode = "FLYCAM",
                LocationName = "Trung tâm Hội nghị Quốc gia",
                SubAddress = "Phạm Hùng, Mễ Trì, Nam Từ Liêm, Hà Nội",
                Latitude = 21.006800m,
                Longitude = 105.787600m,
                ClientTotalRevenue = 5000000m,
                ExternalWorkersNote = "Nam fly phụ",
                Note = "Bay toàn cảnh sảnh ngoài trời",
                Assignments = new List<JobAssignment>
                {
                    new JobAssignment { UserId = hai.Id, CastAmount = 2500000m, IsPaid = false },
                    new JobAssignment { UserId = hung.Id, CastAmount = 2500000m, IsPaid = false }
                }
            }
        };

        await context.EventJobs.AddRangeAsync(jobs);
        await context.SaveChangesAsync();
    }
}
