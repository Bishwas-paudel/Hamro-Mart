using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using HamroMart.Models;
using HamroMart.Data;
using HamroMart.Services;
using HamroMart.ViewModels;

namespace HamroMart.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<AccountController> _logger;
        private readonly IConfiguration _configuration;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ApplicationDbContext context,
            IEmailService emailService,
            ILogger<AccountController> logger,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _emailService = emailService;
            _logger = logger;
            _configuration = configuration;
        }

        // GET: /Account/Register
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        // POST: /Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Check if email already exists
                    var existingUser = await _userManager.FindByEmailAsync(model.Email);
                    if (existingUser != null)
                    {
                        ModelState.AddModelError("Email", "Email is already registered.");
                        return View(model);
                    }

                    // Generate OTP
                    var otp = GenerateOTP();
                    var otpExpiryMinutes = _configuration.GetValue<int>("AppSettings:OTPExpiryMinutes");

                    // Save OTP to database
                    var otpVerification = new OTPVerification
                    {
                        Email = model.Email,
                        OTP = otp,
                        CreatedAt = DateTime.Now,
                        ExpiresAt = DateTime.Now.AddMinutes(otpExpiryMinutes),
                        IsUsed = false
                    };

                    _context.OTPVerifications.Add(otpVerification);
                    await _context.SaveChangesAsync();

                    // Store registration data in TempData for OTP verification
                    TempData["RegistrationData"] = System.Text.Json.JsonSerializer.Serialize(model);
                    TempData["OTPEmail"] = model.Email;

                    // Send OTP email
                    try
                    {
                        await _emailService.SendOTPAsync(model.Email, otp);
                        TempData["SuccessMessage"] = "OTP sent to your email. Please verify to complete registration.";
                        return RedirectToAction("VerifyOTP");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send OTP email");
                        ModelState.AddModelError("", "Failed to send OTP. Please try again.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user registration");
                ModelState.AddModelError("", "An error occurred during registration. Please try again.");
            }

            return View(model);
        }

        // GET: /Account/VerifyOTP
        [HttpGet]
        public IActionResult VerifyOTP()
        {
            if (TempData["OTPEmail"] == null)
            {
                return RedirectToAction("Register");
            }

            var model = new VerifyOTPViewModel
            {
                Email = TempData["OTPEmail"].ToString()
            };
            TempData.Keep("OTPEmail");
            TempData.Keep("RegistrationData");

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyOTP(VerifyOTPViewModel model)
        {
            try
            {
                if (TempData["RegistrationData"] == null)
                {
                    TempData["ErrorMessage"] = "Registration session expired. Please register again.";
                    return RedirectToAction("Register");
                }

                //if (ModelState.IsValid)
                //{
                    // Verify OTP
                    var otpVerification = await _context.OTPVerifications
                        .Where(o => o.Email == model.Email && o.OTP == model.OTP && !o.IsUsed)
                        .OrderByDescending(o => o.CreatedAt)
                        .FirstOrDefaultAsync();

                    if (otpVerification == null)
                    {
                        ModelState.AddModelError("OTP", "Invalid OTP.");
                        return View(model);
                    }

                    if (otpVerification.ExpiresAt < DateTime.Now)
                    {
                        ModelState.AddModelError("OTP", "OTP has expired. Please request a new one.");
                        return View(model);
                    }

                    // Mark OTP as used
                    otpVerification.IsUsed = true;
                    _context.OTPVerifications.Update(otpVerification);
                    await _context.SaveChangesAsync();

                    // Get registration data
                    var registrationData = System.Text.Json.JsonSerializer.Deserialize<RegisterViewModel>(
                        TempData["RegistrationData"].ToString());

                    // Create user
                    var user = new ApplicationUser
                    {
                        UserName = registrationData.Email,
                        Email = registrationData.Email,
                        FirstName = registrationData.FirstName,
                        LastName = registrationData.LastName,
                        PhoneNumber = registrationData.PhoneNumber,
                        Address = registrationData.Address,
                        City = registrationData.City,
                        PostalCode = registrationData.PostalCode,
                        CreatedAt = DateTime.Now,
                        EmailConfirmed = true // Since we verified via OTP
                    };

                    var result = await _userManager.CreateAsync(user, registrationData.Password);

                    if (result.Succeeded)
                    {
                        // Assign user role
                        await _userManager.AddToRoleAsync(user, "User");

                        await _signInManager.SignInAsync(user, isPersistent: false);

                        _logger.LogInformation("User created a new account with password.");

                        TempData["SuccessMessage"] = "Registration completed successfully!";
                        return RedirectToAction("Index", "Home");
                    }

                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
              //  }

                // Restore TempData if validation fails
                TempData.Keep("OTPEmail");
                TempData.Keep("RegistrationData");
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during OTP verification");
                TempData["ErrorMessage"] = "An error occurred during verification. Please try again.";
                return RedirectToAction("Register");
            }
        }

        // POST: /Account/ResendOTP
        [HttpPost]
        public async Task<IActionResult> ResendOTP(string email)
        {
            try
            {
                // Generate new OTP
                var otp = GenerateOTP();
                var otpExpiryMinutes = _configuration.GetValue<int>("AppSettings:OTPExpiryMinutes");

                // Save new OTP to database
                var otpVerification = new OTPVerification
                {
                    Email = email,
                    OTP = otp,
                    CreatedAt = DateTime.Now,
                    ExpiresAt = DateTime.Now.AddMinutes(otpExpiryMinutes),
                    IsUsed = false
                };

                _context.OTPVerifications.Add(otpVerification);
                await _context.SaveChangesAsync();

                // Send new OTP email
                await _emailService.SendOTPAsync(email, otp);

                TempData["SuccessMessage"] = "New OTP sent to your email.";
                TempData.Keep("OTPEmail");
                TempData.Keep("RegistrationData");

                return Json(new { success = true, message = "New OTP sent successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resending OTP");
                return Json(new { success = false, message = "Failed to resend OTP. Please try again." });
            }
        }

        // GET: /Account/Login
        [HttpGet]
        public IActionResult Login(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            try
            {
        
                    var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);

                    if (result.Succeeded)
                    {
                        _logger.LogInformation("User logged in.");

                        // Get the user to check roles
                        var user = await _userManager.FindByEmailAsync(model.Email);
                        if (user != null)
                        {
                            // Check if user is admin
                            if (await _userManager.IsInRoleAsync(user, "Admin"))
                            {
                                return RedirectToAction("Dashboard", "Admin");
                            }
                        }

                        return RedirectToLocal(returnUrl);
                    }
                    else
                    {
                        ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                        return View(model);
                    }
                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user login");
                ModelState.AddModelError(string.Empty, "An error occurred during login. Please try again.");
            }

            return View(model);
        }

        // POST: /Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            _logger.LogInformation("User logged out.");
            return RedirectToAction("Index", "Home");
        }

        // GET: /Account/Profile
        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            var model = new ProfileViewModel
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Address = user.Address,
                City = user.City,
                PostalCode = user.PostalCode
            };

            return View(model);
        }

        // POST: /Account/Profile
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(ProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            // Update user properties
            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.PhoneNumber = model.PhoneNumber;
            user.Address = model.Address;
            user.City = model.City;
            user.PostalCode = model.PostalCode;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return View(model);
            }

            await _signInManager.RefreshSignInAsync(user);
            TempData["SuccessMessage"] = "Your profile has been updated";

            return RedirectToAction(nameof(Profile));
        }

        private string GenerateOTP()
        {
            var random = new Random();
            return random.Next(100000, 999999).ToString();
        }

        private IActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return RedirectToAction("Index", "Home");
            }
        }
    }
}