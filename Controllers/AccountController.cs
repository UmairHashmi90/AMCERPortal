using ERPaperless.Abstractions.Security;
using ERPaperless.Infrastructure.Security;
using ERPaperless.Models;
using ERPaperless.Services;
using System;
using System.Collections.Concurrent;
using System.Web.Mvc;
using System.Web.Security;

namespace ERPaperless.Controllers
{
    [AllowAnonymous]
    public class AccountController : Controller
    {
        private readonly ICurrentUserContext _currentUserContext;
      
        private const int MaxFailedAttempts = 5;
        private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(10);
        private static readonly ConcurrentDictionary<string, LoginAttemptInfo> LoginAttempts
            = new ConcurrentDictionary<string, LoginAttemptInfo>();

        public AccountController(ICurrentUserContext currentUserContext = null)
        {
            _currentUserContext = currentUserContext ?? new CurrentUserContext();
        }

        // ── GET /Account/Login ────────────────────────────────────────────────
        [HttpGet]
        [OutputCache(NoStore = true, Duration = 0, VaryByParam = "*")]
        public ActionResult Login(string returnUrl)
        {
            if (Request.IsAuthenticated)
                return RedirectToLocal(returnUrl);

            SetSecurityHeaders();
            ViewBag.ReturnUrl = returnUrl;
            return View(new LoginViewModel());
        }

        // ── POST /Account/Login ───────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OutputCache(NoStore = true, Duration = 0, VaryByParam = "*")]
        public ActionResult Login(LoginViewModel model, string returnUrl)
        {
            SetSecurityHeaders();

            if (!ModelState.IsValid)
                return View(model);

            // Brute-force lockout check
            var key = BuildAttemptKey(model.Username);
            if (IsLockedOut(key, out var remainingSeconds))
            {
                ModelState.AddModelError(string.Empty,
                    $"Too many failed attempts. Try again in {remainingSeconds} seconds.");
                return View(model);
            }

            // Validate against tblUser + load role from tblERRoleRights
            var user = UserRepository.ValidateAndGetUser(model.Username, model.Password);
            if (user == null)
            {
                RegisterFailedAttempt(key);
                ModelState.AddModelError(string.Empty, "Invalid username or password.");
                return View(model);
            }

            ResetAttempts(key);

            // Clear any data from the previous session to prevent session-fixation.
            // We intentionally keep the session alive (no Abandon) so we can write
            // the freshly-loaded user model into it before the redirect.
            Session.Clear();

            // ── Persist identity in session ──────────────────────────────────
            // Storing the full ERUserRoleModel here means:
            //   • BaseController.CurrentUserRole.UserCode  is available on every
            //     subsequent request (used as intCreatedByCode / intAlteredByCode).
            //   • No extra DB round-trip is needed on the first post-login request.
            //   • BaseController.OnActionExecuting reads from this session entry.
            _currentUserContext.SetCurrentUser(Session, user);

            FormsAuthentication.SetAuthCookie(model.Username, model.RememberMe);
            return RedirectToLocal(returnUrl);
        }

        // ── POST /Account/Logout ──────────────────────────────────────────────
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();
            Session.Clear();
            Session.Abandon();
            _currentUserContext.ClearCurrentUser(Session);
            return RedirectToAction("Login", "Account");
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private static string BuildAttemptKey(string username)
        {
            var safe = (username ?? string.Empty).Trim().ToLowerInvariant();
            return $"{safe}|{GetClientAddress()}";
        }

        private static string GetClientAddress()
            => System.Web.HttpContext.Current?.Request?.UserHostAddress ?? "unknown";

        private static bool IsLockedOut(string key, out int remainingSeconds)
        {
            remainingSeconds = 0;
            if (!LoginAttempts.TryGetValue(key, out var info)) return false;
            if (info.LockoutUntilUtc <= DateTime.UtcNow)       return false;
            remainingSeconds = (int)Math.Ceiling(
                (info.LockoutUntilUtc - DateTime.UtcNow).TotalSeconds);
            return true;
        }

        private static void RegisterFailedAttempt(string key)
        {
            LoginAttempts.AddOrUpdate(key,
                _ => new LoginAttemptInfo { FailedCount = 1, LockoutUntilUtc = DateTime.MinValue },
                (_, existing) =>
                {
                    var next    = existing.FailedCount + 1;
                    var lockout = next >= MaxFailedAttempts
                        ? DateTime.UtcNow.Add(LockoutDuration)
                        : DateTime.MinValue;
                    return new LoginAttemptInfo
                    {
                        FailedCount     = next >= MaxFailedAttempts ? 0 : next,
                        LockoutUntilUtc = lockout
                    };
                });
        }

        private static void ResetAttempts(string key)
            => LoginAttempts.TryRemove(key, out _);

        private ActionResult RedirectToLocal(string returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            var user = _currentUserContext.GetCurrentUser(HttpContext);
            if (user != null)
            {
                if (user.IsPharmacyOnly)
                    return RedirectToAction("PatientForm", "Pharmacy");
                if (user.IsBillingOnly)
                    return RedirectToAction("PatientForm", "Billing");
            }

            return RedirectToAction("Patients", "Home");
        }

        private void SetSecurityHeaders()
        {
            Response.Headers["X-Frame-Options"]        = "DENY";
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            Response.Headers["Referrer-Policy"]        = "strict-origin-when-cross-origin";
            Response.Headers["Cache-Control"]          = "no-store, no-cache, must-revalidate";
            Response.Headers["Pragma"]                 = "no-cache";
            Response.Headers["Expires"]                = "0";
        }

        private class LoginAttemptInfo
        {
            public int      FailedCount     { get; set; }
            public DateTime LockoutUntilUtc { get; set; }
        }
    }
}
