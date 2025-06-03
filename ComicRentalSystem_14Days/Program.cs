using ComicRentalSystem_14Days.Helpers;
using ComicRentalSystem_14Days.Interfaces;
using ComicRentalSystem_14Days.Logging;
using ComicRentalSystem_14Days.Services;
using ComicRentalSystem_14Days.Forms;

namespace ComicRentalSystem_14Days
{
    internal static class Program
    {
        public static ILogger? AppLogger { get; private set; }
        public static FileHelper? AppFileHelper { get; private set; }
        public static ComicService? AppComicService { get; private set; }
        public static MemberService? AppMemberService { get; private set; }
        public static IReloadService? AppReloadService { get; private set; }
        public static AuthenticationService? AppAuthService { get; private set; }


        [STAThread]
        static async Task Main() // Changed to async Task
        {
            ApplicationConfiguration.Initialize();

            AppLogger = new FileLogger("ComicRentalSystemLog.txt");
            AppLogger.Log("應用程式啟動中...");

            AppFileHelper = new FileHelper();
            AppReloadService = new ReloadService();

            try // Added try for async initialization
            {
                if (AppFileHelper != null && AppLogger != null)
                {
                    // Initialize services asynchronously
                    AppComicService = await ComicService.CreateAsync(AppFileHelper, AppLogger);
                    AppMemberService = await MemberService.CreateAsync(AppFileHelper, AppLogger, AppComicService);
                    AppAuthService = new AuthenticationService(AppFileHelper, AppLogger); // AuthService constructor is not async
                    AppAuthService.EnsureAdminUserExists("admin", "admin123");
                }
                else
                {
                    // This case might be less likely if AppLogger or AppFileHelper itself fails, but good for robustness
                    MessageBox.Show("無法初始化基礎輔助程式 (Logger/FileHelper)，應用程式即將關閉。", "嚴重錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    AppLogger?.LogError("由於基礎輔助程式 (Logger/FileHelper) 初始化失敗，應用程式已終止。");
                    return; // Exit if essential helpers are null
                }

                Application.ThreadException += new System.Threading.ThreadExceptionEventHandler(Application_ThreadException);
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);

            // This try-catch remains for Application.Run and other synchronous parts of Main
            // The async initialization is handled by the try-catch block above.
            if (AppLogger != null && AppAuthService != null && AppComicService != null && AppMemberService != null && AppReloadService != null)
            {
                Application.Run(new LoginForm(AppLogger, AppAuthService, AppComicService, AppMemberService, AppReloadService));
            }
            else
            {
                // This else block will now primarily catch issues if the async initialization above failed
                // and one of the services is null.
                MessageBox.Show("無法初始化核心服務，應用程式即將關閉。", "嚴重錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                AppLogger?.LogError("由於核心服務 (Logger、AuthService、ComicService、MemberService 或 ReloadService) 初始化失敗或未正確載入，應用程式已終止。");
            }
        }
            catch (Exception ex) // Catches exceptions from async initialization or other setup
            {
                AppLogger?.LogError("由於 Main 中發生未處理的例外狀況 (可能在初始化期間)，應用程式已終止。", ex);
                MessageBox.Show($"應用程式發生嚴重錯誤，即將關閉。\n錯誤: {ex.Message}\n詳情請查看日誌檔案。", "嚴重錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                AppLogger?.Log("應用程式關閉中。");
            }
        }

        private static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            AppLogger?.LogError("未處理的UI執行緒例外狀況", e.Exception);
            MessageBox.Show($"發生未預期的UI執行緒錯誤: {e.Exception.Message}\n詳情請查看日誌。", "UI 錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            AppLogger?.LogError("未處理的非UI執行緒例外狀況", e.ExceptionObject as Exception);
            if (e.IsTerminating)
            {
                AppLogger?.Log("由於發生未處理的例外狀況，應用程式正在終止。");
            }
        }
    }
}