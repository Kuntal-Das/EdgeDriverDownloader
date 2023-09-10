using Microsoft.Win32;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;

namespace EdgeDriverDownloader
{
    public class EdgeDriverDownloader
    {
        private static EdgeDriverDownloader _instance;
        private const string _edgeVersionFile = "edgeVersion.txt";
        private const string _edgeDriverExeName = "msedgedriver.exe";
        private const string _edgeDiverUrl = @"https://msedgedriver.azureedge.net/{0}/{1}";

        private readonly string _buildType;
        private readonly HttpClient _httpClient;
        private string ZipFileName => $"edgedriver_{_buildType}.zip";
        private string ExtractFolderName => $@"edgedriver_{_buildType}";


        public static EdgeDriverDownloader GetInstance()
        {
            if (_instance is null)
                _instance = new EdgeDriverDownloader();
            return _instance;
        }

        private EdgeDriverDownloader()
        {
            _httpClient = new HttpClient();
            Assembly assembly = Assembly.GetExecutingAssembly();
            assembly.ManifestModule.GetPEKind(out var peKind, out var imageFileMachine);
            if (peKind == PortableExecutableKinds.PE32Plus || imageFileMachine == ImageFileMachine.AMD64 || imageFileMachine == ImageFileMachine.IA64)
                _buildType = "win64";
            else
                _buildType = "win32";
        }

        public async Task DownloadCompatibleDriverAsync(string downloadPath = ".", int trial = 0)
        {
            string fileToSaveVerInfo = Path.Combine(downloadPath, _edgeVersionFile);
            try
            {
                var currEdgeVersion = GetEdgeVersionForWindows();
                if (!await IsDownloadRequiredAsync(currEdgeVersion, downloadPath, fileToSaveVerInfo))
                {
                    return;
                }

                Directory.CreateDirectory(downloadPath);
                //var downloadedZipFullPath = string.IsNullOrEmpty(downloadPath) ? ZipFileName
                //    : Path.Combine(downloadPath, ZipFileName);
                //var extractFolderFullPath = string.IsNullOrEmpty(downloadPath) ? ExtractFolderName
                //    : Path.Combine(downloadPath, ExtractFolderName);

                await DownloadFileAsync(downloadPath, string.Format(_edgeDiverUrl, currEdgeVersion, ZipFileName));

                //ExtractAndCleanUp(downloadPath, downloadedZipFullPath, extractFolderFullPath, _edgeDriverExeName);

                await SaveDownloadedEdgeDriverVersionAsync(currEdgeVersion, fileToSaveVerInfo);
            }
            catch (Exception)
            {
                if (trial > 2) throw;

                CleanUp(ExtractFolderName, downloadPath);
                await DownloadCompatibleDriverAsync(downloadPath, trial + 1);
            }
        }

        private void CleanUp(string extractFolder, string downloadPath)
        {
            try
            {
                if (Directory.Exists(extractFolder)) Directory.Delete(extractFolder, true);
                if (File.Exists(downloadPath)) File.Delete((downloadPath));
            }
            catch (Exception) { }
        }

        private async Task SaveDownloadedEdgeDriverVersionAsync(Version edgeVersion, string fileToSaveVerInfo)
        {
            using (StreamWriter sw = new StreamWriter(File.Open(fileToSaveVerInfo, FileMode.Create), System.Text.Encoding.Unicode))
                await sw.WriteLineAsync(edgeVersion.ToString());
        }

        private void ExtractAndCleanUp(string downloadPath, string downloadedZipFullPath, string extractFolderFullPath)
        {
            var dirInfo = new DirectoryInfo(downloadPath);
            if (dirInfo.Exists)
                dirInfo.Delete(true);


            ZipFile.ExtractToDirectory(downloadPath, extractFolderFullPath);

            File.Copy(Path.Combine(extractFolderFullPath, _edgeDriverExeName),
                Path.Combine(downloadPath, _edgeDriverExeName));

            File.Delete(downloadPath);
            Directory.Delete(extractFolderFullPath, true);
        }

        private async Task DownloadFileAsync(string downloadPath, string edgeDiverUrl)
        {
            var response = await _httpClient.GetAsync(edgeDiverUrl);

            if (response.IsSuccessStatusCode)
            {
                var zipFilePath = string.IsNullOrEmpty(downloadPath) ? ZipFileName
                    : Path.Combine(downloadPath, ZipFileName);

                using (var fs = new FileStream(zipFilePath, FileMode.Create))
                using (ZipArchive zip = new ZipArchive(fs))
                {
                    //await response.Content.CopyToAsync(fs);
                    var edgeDriverEntry = zip.GetEntry(_edgeDriverExeName);

                    edgeDriverEntry?.ExtractToFile(downloadPath, true);
                }
            }
        }

        private async Task<bool> IsDownloadRequiredAsync(Version currEdgeVersion, string downloadPath, string filetoSaveVerInfo)
        {
            if (!string.IsNullOrEmpty(downloadPath) && !Directory.Exists(downloadPath)) return true;

            if (!File.Exists(string.IsNullOrEmpty(downloadPath) ? _edgeDriverExeName
                : Path.Combine(downloadPath, _edgeDriverExeName))) return true;

            string? strPrevEdgeDriverVersion = null;
            if (File.Exists(filetoSaveVerInfo))
            {
                using (StreamReader sr = new StreamReader(filetoSaveVerInfo))
                {
                    strPrevEdgeDriverVersion = await sr.ReadLineAsync();
                }
            }
            if (string.IsNullOrEmpty(strPrevEdgeDriverVersion) ||
                !Version.TryParse(strPrevEdgeDriverVersion, out var prevEdgeDriverVersion))
            {
                return true;
            }
            return prevEdgeDriverVersion != currEdgeVersion;
        }

        private Version GetEdgeVersionForWindows()
        {
            //WINDOWS only
            object version = Registry.GetValue(@"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Edge\BLBeacon", "version", null);

            if (!string.IsNullOrEmpty(version.ToString()) && Version.TryParse(version.ToString(), out var value))
            {
                return value;
            }
            return new Version();
        }
    }
}