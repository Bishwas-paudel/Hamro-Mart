using Microsoft.AspNetCore.Identity;
using HamroMart.Data;
using HamroMart.Models;

namespace HamroMart
{
    public static class DbInitializer
    {
        public static async Task Initialize(ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            // Create database if it doesn't exist
            context.Database.EnsureCreated();

            // Create roles
            string[] roleNames = { "Admin", "User" };
            foreach (var roleName in roleNames)
            {
                var roleExist = await roleManager.RoleExistsAsync(roleName);
                if (!roleExist)
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // Create admin user
            var adminUser = await userManager.FindByEmailAsync("admin@hamromart.com");
            if (adminUser == null)
            {
                var user = new ApplicationUser
                {
                    UserName = "admin@hamromart.com",
                    Email = "admin@hamromart.com",
                    FirstName = "Admin",
                    LastName = "User",
                    Address = "Admin Address",
                    City = "Kathmandu",
                    PostalCode = "44600",
                    EmailConfirmed = true
                };

                var createPowerUser = await userManager.CreateAsync(user, "Admin@123");
                if (createPowerUser.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, "Admin");
                }
            }

            
        }
    }
}