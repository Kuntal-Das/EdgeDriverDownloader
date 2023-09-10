using Microsoft.Win32;
using System.IO.Compression;
using System.Reflection;

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

        public static EdgeDriverDownloader GetInstance()
        {
            _instance ??= new EdgeDriverDownloader();
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

                await DownloadFileAsync(downloadPath, string.Format(_edgeDiverUrl, currEdgeVersion, ZipFileName));

                await SaveDownloadedEdgeDriverVersionAsync(currEdgeVersion, fileToSaveVerInfo);
            }
            catch (Exception)
            {
                if (trial > 2) throw;

                var egDvrFileInfo = new FileInfo(Path.Combine(downloadPath, _edgeDriverExeName));
                if (egDvrFileInfo.Exists) { egDvrFileInfo.Delete(); }

                await DownloadCompatibleDriverAsync(downloadPath, trial + 1);
            }
        }

        private static async Task SaveDownloadedEdgeDriverVersionAsync(Version edgeVersion, string fileToSaveVerInfo)
        {
            using StreamWriter sw = new(File.Open(fileToSaveVerInfo, FileMode.Create), System.Text.Encoding.Unicode);
            await sw.WriteLineAsync(edgeVersion.ToString());
        }

        private async Task DownloadFileAsync(string downloadPath, string edgeDiverUrl)
        {
            var dirInfo = new DirectoryInfo(downloadPath);
            if (!dirInfo.Exists) dirInfo.Create();

            using var response = await _httpClient.GetStreamAsync(edgeDiverUrl);
            using ZipArchive zip = new(response, ZipArchiveMode.Read);
            var edgeDriverEntry = zip.GetEntry(_edgeDriverExeName);
            edgeDriverEntry?.ExtractToFile(Path.Combine(downloadPath, _edgeDriverExeName), true);
        }


        private static async Task<bool> IsDownloadRequiredAsync(Version currEdgeVersion, string downloadPath, string filetoSaveVerInfo)
        {
            if (!string.IsNullOrEmpty(downloadPath) && !Directory.Exists(downloadPath)) return true;

            if (!File.Exists(string.IsNullOrEmpty(downloadPath) ? _edgeDriverExeName
                : Path.Combine(downloadPath, _edgeDriverExeName))) return true;

            string? strPrevEdgeDriverVersion = null;
            if (File.Exists(filetoSaveVerInfo))
            {
                using StreamReader sr = new(filetoSaveVerInfo);
                strPrevEdgeDriverVersion = await sr.ReadLineAsync();
            }
            if (string.IsNullOrEmpty(strPrevEdgeDriverVersion) ||
                !Version.TryParse(strPrevEdgeDriverVersion, out var prevEdgeDriverVersion))
            {
                return true;
            }
            return prevEdgeDriverVersion != currEdgeVersion;
        }

        private static Version GetEdgeVersionForWindows()
        {
            //WINDOWS only
            var version = Registry.GetValue(@"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Edge\BLBeacon", "version", null);

            if (!string.IsNullOrEmpty(version?.ToString()) && Version.TryParse(version?.ToString(), out var value))
            {
                return value;
            }
            return new Version();
        }
    }
}