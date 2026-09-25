using Blood_Donations_Project.Common;
using Blood_Donations_Project.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Blood_Donations_Project.Services
{
    public class SeedService
    {
        public static async Task SeedDatabase(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BloodDonationContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<SeedService>>();

            try
            {
                logger.LogInformation("Starting database seeding...");

                var canConnect = await context.Database.CanConnectAsync();
                if (!canConnect)
                {
                    logger.LogError("Cannot connect to database!");
                    return;
                }

                logger.LogInformation("Database connection successful!");

                await SeedRoles(context, logger);

                await SeedAdminUser(context, logger);

                await SeedBloodTypes(context, logger);

                await SeedBloodInventory(context, logger);

                logger.LogInformation("Database seeding completed successfully!");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during database seeding: {Message}", ex.Message);
            }
        }

        private static async Task SeedRoles(BloodDonationContext context, ILogger logger)
        {
            try
            {
                var existingRolesCount = await context.Roles.CountAsync();

                if (existingRolesCount > 0)
                {
                    logger.LogInformation($"Roles already exist ({existingRolesCount} roles found). Skipping...");
                    return;
                }

                logger.LogInformation("Adding roles...");

                var roles = new List<Role>
                {
                    new Role { RoleName = AppRoles.Admin },
                    new Role { RoleName = AppRoles.Donor },
                    new Role { RoleName = AppRoles.Hospital },
                    new Role { RoleName = AppRoles.BloodBank }
                };

                await context.Roles.AddRangeAsync(roles);
                await context.SaveChangesAsync();

                logger.LogInformation("4 Roles added successfully!");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error seeding roles: {Message}", ex.Message);
            }
        }

        private static async Task SeedBloodTypes(BloodDonationContext context, ILogger logger)
        {
            try
            {
                logger.LogInformation("Seeding blood types (add missing only)...");

                var required = new[] { "A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-" };

                var existing = await context.BloodTypes
                    .Select(b => b.TypeName)
                    .ToListAsync();

                var missing = required
                    .Where(t => !existing.Contains(t))
                    .Select(t => new BloodType { TypeName = t })
                    .ToList();

                if (!missing.Any())
                {
                    logger.LogInformation("All blood types already exist. Skipping...");
                    return;
                }

                await context.BloodTypes.AddRangeAsync(missing);
                await context.SaveChangesAsync();

                logger.LogInformation($"Added {missing.Count} missing blood types successfully!");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error seeding blood types: {Message}", ex.Message);
            }
        }

        private static async Task SeedAdminUser(BloodDonationContext context, ILogger logger)
        {
            try
            {
                var adminEmail = "admin@gmail.com";

                var adminExists = await context.Users.AnyAsync(u => u.Email == adminEmail);

                if (adminExists)
                {
                    logger.LogInformation($"Admin user already exists ({adminEmail}). Skipping...");
                    return;
                }

                logger.LogInformation("Adding admin user...");

                var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.RoleName == AppRoles.Admin);

                if (adminRole == null)
                {
                    logger.LogError("Admin role not found! Cannot create admin user.");
                    return;
                }

                var passwordHasher = new PasswordHasher<User>();

                var adminUser = new User
                {
                    FullName = "Shatha Ammar",
                    UserName = adminEmail,
                    Email = adminEmail,
                    RoleId = adminRole.RoleId,
                    MobileNo = "0777777777",
                    Address = "Admin Office"
                };

                adminUser.Password = passwordHasher.HashPassword(adminUser, "Admin@123");

                await context.Users.AddAsync(adminUser);
                await context.SaveChangesAsync();

                logger.LogInformation("Admin user created successfully!");
                logger.LogInformation($"   Email: {adminEmail}");
                logger.LogInformation($"   Password: Admin@123");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error seeding admin user: {Message}", ex.Message);
            }
        }

        private static async Task SeedBloodInventory(BloodDonationContext context, ILogger logger)
        {
            try
            {
                var bloodTypes = await context.BloodTypes.ToListAsync();

                foreach (var bt in bloodTypes)
                {
                    var exists = await context.BloodInventories
                        .AnyAsync(i => i.BloodTypeId == bt.BloodTypeId);

                    if (!exists)
                    {
                        await context.BloodInventories.AddAsync(new BloodInventory
                        {
                            BloodTypeId = bt.BloodTypeId,
                            UnitsAvailable = 0
                        });
                    }
                }

                await context.SaveChangesAsync();
                logger.LogInformation("Blood inventory seeded successfully!");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error seeding inventory: {Message}", ex.Message);
            }
        }

    }
}