// ComicRentalSystem_14Days/Constants.cs
namespace ComicRentalSystem_14Days
{
    public static class Constants
    {
        public static class FileNames
        {
            public const string Comics = "comics.csv";
            public const string Members = "members.csv";
            public const string Users = "users.csv"; // Added for AuthenticationService later
            public const string LogFile = "ComicRentalSystemLog.txt";
        }

        public static class ComicStatuses
        {
            public const string Rented = "被借閱";
            public const string Available = "在館中";
            // For dgvMyRentedComics status column, if needed centrally
            public const string OverduePrefix = "逾期 ";
            public const string DueToday = "今日到期";
            public const string RemainingPrefix = "剩餘 ";
            public const string DaysSuffix = " 天";

        }

        public static class Roles
        {
            // While UserRole is an enum, string representations might be used in some places (e.g. config, display)
            public const string Admin = "Admin";
            public const string Member = "Member";
        }

        public static class UI
        {
            public static class Placeholders
            {
                public const string SearchComicsPlaceholder = "依書名/作者搜尋...";
            }

            public static class FilterOptions
            {
                public const string All = "全部";
                public const string Rented = "已租借"; // Matches ComicStatuses.Rented if used directly
                public const string Available = "可租借"; // Different from ComicStatuses.Available, specific to filter
                public const string AllGenres = "所有類型";
            }

            public static class MessageBoxTitles
            {
                public const string Error = "錯誤";
                public const string Information = "提示";
                public const string Success = "成功";
                public const string ConfirmDelete = "確認刪除";
                public const string InsufficientPermissions = "權限不足";
                public const string RentalError = "租借錯誤";
                public const string DeleteError = "刪除錯誤";
                public const string OperationError = "操作錯誤";
                public const string LoadError = "載入錯誤";
                public const string ValidationFailed = "驗證失敗";
                public const string RegistrationFailed = "註冊失敗";
                public const string RegistrationSuccess = "註冊成功";
                 public const string UserNotFound = "找不到使用者";
            }

            public static class FormTitles
            {
                public const string AdminBaseTitle = "漫畫租借系統 - ";
                public const string MemberBaseTitle = "漫畫租借系統";
                public const string Dashboard = "儀表板";
                public const string ComicManagement = "漫畫管理";
            }
        }

        // For DataGridView DataPropertyNames (exact match to model properties)
        public static class DataPropertyNames
        {
            // Common
            public const string Id = "Id";
            public const string Title = "Title";
            public const string Author = "Author";
            public const string Genre = "Genre";
            public const string Isbn = "Isbn";
            public const string IsRented = "IsRented";
            public const string RentedToMemberId = "RentedToMemberId";
            public const string RentalDate = "RentalDate";
            public const string ReturnDate = "ReturnDate"; // ExpectedReturnDate for ViewModel
            public const string ActualReturnTime = "ActualReturnTime";

            // AdminComicStatusViewModel specific
            public const string Status = "Status";
            public const string BorrowerName = "BorrowerName";
            public const string BorrowerPhoneNumber = "BorrowerPhoneNumber";

            // RentalDetailViewModel specific
            public const string ComicTitle = "ComicTitle"; // Note: different from Comic.Title if ViewModel uses different names
            public const string ExpectedReturnDate = "ExpectedReturnDate";

            // Member specific
            public const string Name = "Name";
            public const string PhoneNumber = "PhoneNumber";

        }

        public static class ColumnHeaders
        {
            // Comic DGV (Admin & Member general)
            public const string ID = "ID";
            public const string Title = "書名";
            public const string Author = "作者";
            public const string Genre = "類型";
            public const string ISBN = "ISBN";
            public const string IsRented = "已租借";
            public const string RentedToMemberId = "租借會員ID";
            public const string RentalDate = "租借日期"; // MainForm uses "租借於" for admin
            public const string AdminRentalDate = "租借於";
            public const string ExpectedReturnDate = "預計歸還時間"; // MainForm uses "到期日" for admin
            public const string AdminExpectedReturnDate = "到期日";
            public const string ActualReturnTime = "實際歸還時間";

            // AdminComicStatusViewModel specific for dgvAvailableComics (Admin View)
            public const string Status = "狀態";
            public const string BorrowerName = "借閱者";
            public const string BorrowerPhoneNumber = "借閱者電話";

            // MyRentedComics DGV (Member View)
            // Title and Author are same as above
            public const string MemberRentalDate = "租借日期"; // dgvMyRentedComics
            public const string MemberExpectedReturnDate = "歸還日期"; // dgvMyRentedComics
            public const string MemberStatus = "狀態"; // For the statusColumn in dgvMyRentedComics

            // Member DGV
            public const string Name = "姓名";
            public const string PhoneNumber = "電話號碼";
        }

        public static class LoggingMessages
        {
            // Example, expand as needed
            public const string MainFormLoading = "主表單正在載入...";
            public const string MainFormLoaded = "主表單載入完成。";
            public const string ComicsChangedEventReceived = "收到 ComicsChanged 事件。";
            public const string MembersChangedEventReceived = "收到 MembersChanged 事件。";
        }
    }
}
