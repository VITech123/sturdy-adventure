using EnterpriseWorkReport.Models;

namespace EnterpriseWorkReport.Services
{
    /// <summary>
    /// Holds the currently logged-in user for the session.
    /// </summary>
    public static class SessionManager
    {
        public static User CurrentUser { get; private set; }

        public static void Login(User user)
        {
            CurrentUser = user;
        }

        public static void Logout()
        {
            CurrentUser = null;
        }

        public static bool IsAdmin => CurrentUser != null && CurrentUser.Role == "Administrator";
        public static bool IsLoggedIn => CurrentUser != null;
    }
}
