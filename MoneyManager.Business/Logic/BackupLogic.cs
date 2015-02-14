using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Popups;
using Microsoft.Live;
using MoneyManager.Foundation;
using Xamarin;

namespace MoneyManager.Business.Logic {
    public class BackupLogic {
        public static async Task<LiveConnectClient> LogInToOneDrive() {
            try {
                var authClient = new LiveAuthClient();
                LiveLoginResult result =
                    await
                        authClient.LoginAsync(new[]
                        {"wl.basic", "wl.skydrive", "wl.skydrive_update", "wl.offline_access"});

                if (result.Status == LiveConnectSessionStatus.Connected) {
                    return new LiveConnectClient(result.Session);
                }
            } catch (LiveAuthException) {
                ShowAuthExceptionMessage();
            } catch (LiveConnectException) {
                ShowConnectExceptionMessage();
            }
            return null;
        }

        private static async void ShowAuthExceptionMessage() {
            var dialog = new MessageDialog(Translation.GetTranslation("AuthExceptionMessage"),
                Translation.GetTranslation("AuthException"));
            dialog.Commands.Add(new UICommand(Translation.GetTranslation("OkLabel")));

            await dialog.ShowAsync();
        }

        private static async void ShowConnectExceptionMessage() {
            var dialog = new MessageDialog(Translation.GetTranslation("ConnectionExceptionMessage"),
                Translation.GetTranslation("ConnectionException"));
            dialog.Commands.Add(new UICommand(Translation.GetTranslation("OkLabel")));

            await dialog.ShowAsync();
        }

        public static async Task<TaskCompletionType> UploadBackup(LiveConnectClient liveClient, string folderId,
            string dbName) {
            try {
                StorageFolder localFolder = ApplicationData.Current.LocalFolder;

                StorageFile storageFile = await localFolder.GetFileAsync(dbName);

                LiveUploadOperation uploadOperation = await liveClient.CreateBackgroundUploadAsync(
                    folderId, "backup" + dbName, storageFile, OverwriteOption.Overwrite);

                LiveOperationResult uploadResult = await uploadOperation.StartAsync();

                return TaskCompletionType.Successful;
            } catch (TaskCanceledException ex) {
                Insights.Report(ex);
                return TaskCompletionType.Aborted;
            } catch (Exception ex) {
                Insights.Report(ex);
                return TaskCompletionType.Unsuccessful;
            }
        }

        public static async Task<string> CreateBackupFolder(LiveConnectClient liveClient, string folderName) {
            if (liveClient != null) {
                try {
                    var folderData = new Dictionary<string, object> {{"name", folderName}};
                    LiveOperationResult operationResult = await liveClient.PostAsync("me/skydrive", folderData);
                    dynamic result = operationResult.Result;
                    return result.id;
                } catch (LiveConnectException ex) {
                    Insights.Report(ex, ReportSeverity.Error);
                }
            }
            return String.Empty;
        }

        public static async Task<string> GetFolderId(LiveConnectClient liveClient, string folderName) {
            try {
                LiveOperationResult operationResultFolder = await liveClient.GetAsync("me/skydrive/");
                dynamic toplevelfolder = operationResultFolder.Result;

                operationResultFolder = await liveClient.GetAsync(toplevelfolder.id + "/files");
                dynamic folders = operationResultFolder.Result.Values;

                foreach (dynamic data in folders) {
                    foreach (dynamic folder in data) {
                        if (folder.name == folderName) {
                            return folder.id;
                        }
                    }
                }
            } catch (LiveConnectException ex) {
                Insights.Report(ex, ReportSeverity.Error);
            }
            return String.Empty;
        }

        public static async Task<string> GetBackupId(LiveConnectClient liveClient, string folderId, string backupName) {
            try {
                LiveOperationResult operationResultFolder = await liveClient.GetAsync(folderId + "/files");
                dynamic files = operationResultFolder.Result.Values;

                foreach (dynamic data in files) {
                    foreach (dynamic file in data) {
                        if (file.name == backupName) {
                            return file.id;
                        }
                    }
                }
            } catch (LiveConnectException ex) {
                Insights.Report(ex, ReportSeverity.Error);
            }

            return String.Empty;
        }

        public static async Task<string> GetBackupCreationDate(LiveConnectClient liveClient, string backupId) {
            if (backupId != null) {
                try {
                    LiveOperationResult operationResult =
                        await liveClient.GetAsync(backupId);
                    dynamic result = operationResult.Result;
                    DateTime createdAt = Convert.ToDateTime(result.created_time);
                    return createdAt.ToString("f", new CultureInfo(CultureInfo.CurrentCulture.TwoLetterISOLanguageName));
                } catch (Exception ex) {
                    Insights.Report(ex, ReportSeverity.Error);
                }
            }
            return String.Empty;
        }

        public static async Task<TaskCompletionType> RestoreBackUp(LiveConnectClient liveClient, string folderId,
            string backupName, string dbName) {
            try {
                string backupId = await GetBackupId(liveClient, folderId, backupName);
                StorageFolder localFolder = ApplicationData.Current.LocalFolder;
                StorageFile storageFile =
                    await localFolder.CreateFileAsync(dbName, CreationCollisionOption.ReplaceExisting);

                await liveClient.BackgroundDownloadAsync(backupId + "/content", storageFile);
                return TaskCompletionType.Successful;
            } catch (Exception ex) {
                Insights.Report(ex, ReportSeverity.Error);
                return TaskCompletionType.Unsuccessful;
            }
        }
    }
}