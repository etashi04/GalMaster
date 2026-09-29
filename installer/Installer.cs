using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows.Forms;

internal static class Program
{
    // 새 프로젝트를 만들 때 아래 여섯 값을 먼저 교체한다.
    const string GameName = "GalMaster";
    const string GameExeName = "GAL PRO MASTER.exe";
    const string PlatformAndBuild = "Steam 24775635";
    const string Version = "v1.0.0";
    const string ExeHash = "B2C5510F87CDFEFF4B5B9AE7DCDEB2C29440A98104660F4C277F4AE933369746";
    const string MarkerDir = "_KR_PATCH_v1.0.0";

    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        if (args.Length == 2 && (args[0].Equals("/install", StringComparison.OrdinalIgnoreCase) || args[0].Equals("/restore", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                using (var form = new InstallerForm()) form.RunCommand(args[0], args[1]);
            }
            catch (Exception ex) { Console.Error.WriteLine(ex.Message); Environment.ExitCode = 1; }
            return;
        }
        Application.Run(new InstallerForm());
    }

    sealed class InstallerForm : Form
    {
        readonly TextBox path = new TextBox();
        readonly Label status = new Label();
        bool silent;

        public InstallerForm()
        {
            Text = GameName + " 한국어 패치 " + Version;
            Icon = LoadInstallerIcon();
            ClientSize = new Size(640, 285);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false; MinimizeBox = true; StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(245, 245, 245);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Malgun Gothic", 9F);

            var title = new Label { Text = GameName + "\r\n비공식 한국어 패치", Left = 28, Top = 22, Width = 420, Height = 58, Font = new Font("Malgun Gothic", 15, FontStyle.Bold) };
            // 사진 1의 배치: Steam 상점 상세 페이지 우측 상단 캡슐 이미지를 표시한다.
            // 상점에서 게임을 검색하고 앱 ID를 확인한 뒤 소개문 위 캡슐 원본을 확보한다.
            // 게임식별자_타이틀로고.png를 InstallerLogo 리소스로 내장한다. 웹 로딩은 하지 않는다.
            var logo = new PictureBox { Left = 476, Top = 19, Width = 128, Height = 60, SizeMode = PictureBoxSizeMode.Zoom, Image = LoadLogo(), BackColor = Color.Transparent, BorderStyle = BorderStyle.None };
            var info = new Label { Text = "지원 게임: " + PlatformAndBuild + " · 원본 파일은 자동 백업됩니다.", Left = 28, Top = 91, Width = 585, Height = 24 };
            var folderLabel = new Label { Text = "게임 설치 폴더", Left = 28, Top = 125, Width = 150, Height = 22 };
            path.Left = 28; path.Top = 148; path.Width = 475; path.Height = 25; path.Text = GuessPath();
            var browse = new Button { Text = "찾아보기", Left = 513, Top = 146, Width = 100, Height = 29 };
            browse.Click += delegate { using (var d = new FolderBrowserDialog()) { d.SelectedPath = path.Text; if (d.ShowDialog() == DialogResult.OK) path.Text = d.SelectedPath; } };
            var install = new Button { Text = "한국어 패치 설치", Left = 28, Top = 197, Width = 178, Height = 38, Font = new Font("Malgun Gothic", 9, FontStyle.Bold) };
            var restore = new Button { Text = "원본 복구", Left = 218, Top = 197, Width = 128, Height = 38, Font = new Font("Malgun Gothic", 9, FontStyle.Bold) };
            install.Click += delegate { RunSafe(Install); };
            restore.Click += delegate { RunSafe(Restore); };
            status.Left = 28; status.Top = 245; status.Width = 585; status.Height = 26; status.TextAlign = ContentAlignment.MiddleLeft;
            Controls.AddRange(new Control[] { title, logo, info, folderLabel, path, browse, install, restore, status });
        }

        static Image LoadLogo()
        {
            Stream stream = typeof(Program).Assembly.GetManifestResourceStream("InstallerLogo");
            return stream == null ? null : Image.FromStream(stream);
        }

        static Icon LoadInstallerIcon()
        {
            Stream stream = typeof(Program).Assembly.GetManifestResourceStream("InstallerIcon");
            return stream == null ? null : new Icon(stream);
        }

        public void RunCommand(string command, string targetPath)
        {
            silent = true;
            path.Text = targetPath;
            if (command.Equals("/install", StringComparison.OrdinalIgnoreCase)) Install();
            else Restore();
        }

        string GuessPath()
        {
            string steam = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
            if (!string.IsNullOrEmpty(steam)) {
                string candidate = Path.Combine(steam, @"steamapps\common\GalMaster");
                if (File.Exists(Path.Combine(candidate, GameExeName))) return candidate;
                string libraries = Path.Combine(steam, @"steamapps\libraryfolders.vdf");
                if (File.Exists(libraries)) foreach (string line in File.ReadAllLines(libraries)) {
                    var match = System.Text.RegularExpressions.Regex.Match(line, "\"path\"\\s+\"([^\"]+)\"");
                    if (match.Success) {
                        candidate = Path.Combine(match.Groups[1].Value.Replace(@"\\", @"\"), @"steamapps\common\GalMaster");
                        if (File.Exists(Path.Combine(candidate, GameExeName))) return candidate;
                    }
                }
            }
            return "";
        }

        void RunSafe(Action action)
        {
            try { action(); }
            catch (Exception ex) { status.Text = "실패: " + ex.Message; MessageBox.Show(ex.Message, "작업 실패", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        string ValidateTarget()
        {
            string target = Path.GetFullPath(path.Text.Trim());
            string exe = Path.Combine(target, GameExeName);
            if (!File.Exists(exe)) throw new InvalidOperationException("선택한 폴더에서 " + GameExeName + "를 찾지 못했습니다.");
            using (var sha = SHA256.Create()) using (var stream = File.OpenRead(exe))
            {
                string hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                if (!hash.Equals(ExeHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("지원하지 않는 게임 실행 파일입니다. Steam 파일 무결성 검사를 먼저 해 주세요.");
            }
            foreach (var process in System.Diagnostics.Process.GetProcessesByName("GAL PRO MASTER")) {
                process.Dispose(); throw new InvalidOperationException("게임을 종료한 뒤 다시 시도해 주세요.");
            }
            foreach (string line in ResourceLines("supported.tsv")) {
                string[] fields = line.Split('\t');
                string file = Safe(target, fields[0]);
                if (!File.Exists(file) || Hash(file) != fields[1]) throw new InvalidOperationException("지원 빌드와 파일이 다릅니다: " + fields[0]);
            }
            return target;
        }

        sealed class Item { public string Path, OldHash, NewHash; public bool Existed; }
        static string Hash(string path) {
            using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }
        static string[] ResourceLines(string name) {
            using (var stream = typeof(Program).Assembly.GetManifestResourceStream(name))
            using (var reader = new StreamReader(stream))
                return reader.ReadToEnd().Trim('\uFEFF','\r','\n').Split(new char[]{'\r','\n'}, StringSplitOptions.RemoveEmptyEntries);
        }
        static string Safe(string root, string relative) {
            if (Path.IsPathRooted(relative) || relative.Contains(":")) throw new InvalidDataException("잘못된 파일 경로입니다.");
            string prefix = Path.GetFullPath(root).TrimEnd('\\') + "\\";
            string full = Path.GetFullPath(Path.Combine(prefix, relative));
            if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("게임 폴더 밖의 경로입니다.");
            for (string p = full; p.Length >= prefix.Length; p = Path.GetDirectoryName(p))
                if ((File.Exists(p) || Directory.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("링크로 연결된 패치 경로는 지원하지 않습니다.");
            return full;
        }
        static void Copy(string src, string dst) { Directory.CreateDirectory(Path.GetDirectoryName(dst)); File.Copy(src, dst, true); }
        static void WriteState(string path, List<Item> items) {
            var lines = new List<string>();
            foreach(var x in items) lines.Add(x.Path + "\t" + (x.Existed?"1":"0") + "\t" + x.OldHash + "\t" + x.NewHash);
            File.WriteAllLines(path, lines.ToArray(), new System.Text.UTF8Encoding(false));
        }
        static List<Item> ReadState(string file) {
            var result = new List<Item>(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(string line in File.ReadAllLines(file)) {
                string[] q=line.Split('\t');
                if(q.Length!=4 || !seen.Add(q[0]) || (q[1]!="0" && q[1]!="1") || q[3].Length!=64 || (q[1]=="1" && q[2].Length!=64))
                    throw new InvalidDataException("설치 기록이 손상되었습니다.");
                result.Add(new Item{Path=q[0],Existed=q[1]=="1",OldHash=q[2],NewHash=q[3]});
            }
            return result;
        }
        static void ValidateState(string root, string marker, List<Item> items) {
            var expected = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            foreach(string line in ResourceLines("payload.tsv")) {var q=line.Split('\t');expected.Add(q[0],q[1]);}
            if(items.Count!=expected.Count)throw new InvalidDataException("다른 버전 또는 손상된 설치 기록입니다.");
            foreach(var x in items) {
                if(!expected.ContainsKey(x.Path) || expected[x.Path]!=x.NewHash)throw new InvalidDataException("설치 기록의 파일 목록이 다릅니다.");
                string dst=Safe(root,x.Path), old=Safe(marker,"backup/"+x.Path);
                if(!File.Exists(dst) || Hash(dst)!=x.NewHash)throw new IOException("패치 후 파일이 변경되었습니다. 복구하지 않고 중단합니다: "+x.Path);
                if(x.Existed && (!File.Exists(old) || Hash(old)!=x.OldHash))throw new IOException("원본 백업이 없거나 변경되었습니다: "+x.Path);
            }
        }
        static void Undo(string root, string marker, List<Item> items) {
            for(int i=items.Count-1;i>=0;i--) {
                var x=items[i]; string dst=Safe(root,x.Path);
                if(x.Existed) { if(!File.Exists(dst) || Hash(dst)!=x.OldHash) Copy(Safe(marker,"backup/"+x.Path),dst); }
                else if(File.Exists(dst))File.Delete(dst);
            }
        }
        static void RemoveOwnedDirectory(string root, string relative) {
            string p=Safe(root,relative);
            if(Directory.Exists(p))Directory.Delete(p,true);
        }
        static void PruneEmpty(string root) {
            if(!Directory.Exists(root))return;
            foreach(string d in Directory.GetDirectories(root)) {
                if((File.GetAttributes(d)&FileAttributes.ReparsePoint)!=0)continue;
                PruneEmpty(d);
            }
            if(Directory.GetFileSystemEntries(root).Length==0)Directory.Delete(root);
        }
        void Install() {
            string root=ValidateTarget(), marker=Safe(root,MarkerDir), record=Safe(marker,"state.tsv");
            if(Directory.Exists(marker)) {
                if(!File.Exists(record))throw new IOException("이전 설치가 완료되지 않았습니다. 백업 폴더를 보존한 채 문의해 주세요.");
                ValidateState(root,marker,ReadState(record));status.Text="이미 같은 버전이 설치되어 있습니다.";return;
            }
            var items=new List<Item>();
            foreach(string line in ResourceLines("payload.tsv")) {
                var q=line.Split('\t');string dst=Safe(root,q[0]);
                if(Directory.Exists(dst))throw new IOException("파일 위치에 폴더가 있습니다: "+q[0]);
                bool exists=File.Exists(dst);string old=exists?Hash(dst):"";
                if(exists && old!=q[1])throw new IOException("기존 모드 또는 패치 파일과 충돌합니다. 이전 패치를 복구한 뒤 설치하세요: "+q[0]);
                items.Add(new Item{Path=q[0],Existed=exists,OldHash=old,NewHash=q[1]});
            }
            string tempName="GalMasterKR_"+Guid.NewGuid().ToString("N"), temp=Safe(Path.GetTempPath(),tempName);
            var changed=new List<Item>(); bool successful=false;
            try {
                Directory.CreateDirectory(temp);
                using(var stream=typeof(Program).Assembly.GetManifestResourceStream("payload.zip"))
                using(var zip=new ZipArchive(stream,ZipArchiveMode.Read)) {
                    foreach(var entry in zip.Entries) {
                        string dst=Safe(temp,entry.FullName);
                        if(entry.FullName.EndsWith("/"))continue;
                        Directory.CreateDirectory(Path.GetDirectoryName(dst));
                        using(var input=entry.Open())using(var output=File.Create(dst))input.CopyTo(output);
                    }
                }
                foreach(var x in items)if(Hash(Safe(temp,x.Path))!=x.NewHash)throw new IOException("내장 파일 검증 실패: "+x.Path);
                Directory.CreateDirectory(marker);
                foreach(var x in items)if(x.Existed)Copy(Safe(root,x.Path),Safe(marker,"backup/"+x.Path));
                // Persist original information before touching destination files.
                WriteState(record,items);
                foreach(var x in items) { changed.Add(x); Copy(Safe(temp,x.Path),Safe(root,x.Path)); }
                ValidateState(root,marker,items);successful=true;
            } catch {
                if(Directory.Exists(marker)) {
                    Undo(root,marker,changed);
                    RemoveOwnedDirectory(root,MarkerDir);
                }
                throw;
            } finally { RemoveOwnedDirectory(Path.GetTempPath(),tempName); }
            if(successful) {status.Text="한국어 패치 설치가 완료되었습니다."; if(!silent)MessageBox.Show(status.Text,"완료",MessageBoxButtons.OK,MessageBoxIcon.Information);}
        }
        void Restore() {
            string root=ValidateTarget(), marker=Safe(root,MarkerDir), record=Safe(marker,"state.tsv");
            if(!File.Exists(record))throw new IOException("이 설치기의 설치 기록이 없습니다. 수동 설치판은 README의 제거 방법을 이용하세요.");
            var items=ReadState(record);ValidateState(root,marker,items);
            string tempName="GalMasterKR_"+Guid.NewGuid().ToString("N"), temp=Safe(Path.GetTempPath(),tempName);
            try {
                foreach(var x in items)Copy(Safe(root,x.Path),Safe(temp,x.Path));
                try { Undo(root,marker,items); }
                catch { foreach(var x in items)Copy(Safe(temp,x.Path),Safe(root,x.Path));throw; }
                RemoveOwnedDirectory(root,MarkerDir);
                PruneEmpty(Safe(root,"BepInEx"));
            } finally {RemoveOwnedDirectory(Path.GetTempPath(),tempName);}
            status.Text="원본 복구가 완료되었습니다. 실행 중 생성된 로그와 설정은 보존됩니다.";
            if(!silent)MessageBox.Show(status.Text,"완료",MessageBoxButtons.OK,MessageBoxIcon.Information);
        }
    }
}
