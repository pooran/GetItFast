using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Forms;
using System.IO;
using System.ComponentModel;
using Aspera.Transfer;
using System.Diagnostics;
namespace GetItFast
{

    public class FileStatus
    {
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public bool IsUploaded { get; set; }
        public bool IsArchived { get; set; }
        public DateTime UploadStartTime { get; set; }
        public DateTime UploadEndTime { get; set; }
        public string Error { get; set; }
        public FileStatus()
        {
            IsUploaded = false;
            IsArchived = false;
        }
    }
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        static TextWriterTraceListener logWriter = null;

        public bool IsLoggedIn { get; set; }
        private System.Windows.Forms.NotifyIcon m_notifyIcon;
        public static FileStatus currentFile = new FileStatus();
        private StringBuilder m_Sb;
        private static bool m_bDirty;
        private System.IO.FileSystemWatcher m_Watcher;
        private bool m_bIsWatching;
        public static string strArchivePath = "";
        List<FileStatus> FilesToArchive = new List<FileStatus>();

        public MainWindow()
        {
            InitializeComponent();
            IsLoggedIn = false;
            Login.Visibility = Visibility.Visible;
            m_Sb = new StringBuilder();
            m_bDirty = false;
            m_bIsWatching = false;
            m_notifyIcon = new System.Windows.Forms.NotifyIcon();
            m_notifyIcon.BalloonTipText = "The app has been minimised. Click the tray icon to show.";
            m_notifyIcon.BalloonTipTitle = "The App";
            m_notifyIcon.Text = "The App";
            m_notifyIcon.Icon = new System.Drawing.Icon("favicon.ico");
            m_notifyIcon.Click += m_notifyIcon_Click;
        }

        void OnClose(object sender, CancelEventArgs args)
        {
            m_notifyIcon.Dispose();
            m_notifyIcon = null;
            if (FaspManager.getInstance().getSessionIDList().Count == 1)
            {
                FaspManager.destroy();
            }
        }

        private WindowState m_storedWindowState = WindowState.Normal;
        void OnStateChanged(object sender, EventArgs args)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
                if (m_notifyIcon != null)
                    m_notifyIcon.ShowBalloonTip(2000);
            }
            else
                m_storedWindowState = WindowState;
        }
        void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs args)
        {
            CheckTrayIcon();
        }

        void m_notifyIcon_Click(object sender, EventArgs e)
        {
            Show();
            WindowState = m_storedWindowState;
        }
        void CheckTrayIcon()
        {
            ShowTrayIcon(!IsVisible);
        }

        void ShowTrayIcon(bool show)
        {
            if (m_notifyIcon != null)
                m_notifyIcon.Visible = show;
        }



        private void Label_MouseUp(object sender, MouseButtonEventArgs e)
        {
            this.Close();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (btnMonitor.Content.ToString() == "Stop Monitoring")
                btnMonitor.Content = "Start Monitoring";
            else
                btnMonitor.Content = "Stop Monitoring";
            strArchivePath = strArchiveFolderPath.Text;
            if (m_bIsWatching)
            {
                m_bIsWatching = false;
                m_Watcher.EnableRaisingEvents = false;
                m_Watcher.Dispose();

            }
            else
            {
                m_bIsWatching = true;

                m_Watcher = new System.IO.FileSystemWatcher();
                if ((bool)selectFolder.IsChecked)
                {
                    m_Watcher.Filter = "*.*";
                    m_Watcher.Path = strFolderPath.Text + "\\";
                }
                else
                {
                    m_Watcher.Filter = strFilePath.Text.Substring(strFilePath.Text.LastIndexOf('\\') + 1);
                    m_Watcher.Path = strFilePath.Text.Substring(0, strFilePath.Text.Length - m_Watcher.Filter.Length);
                }

                if ((bool)selectFolder.IsChecked)
                {
                    m_Watcher.IncludeSubdirectories = true;
                }

                m_Watcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite
                                     | NotifyFilters.FileName | NotifyFilters.DirectoryName;
                //m_Watcher.Changed += new FileSystemEventHandler(OnChanged);
                m_Watcher.Created += new FileSystemEventHandler(OnChanged);
                //m_Watcher.Deleted += new FileSystemEventHandler(OnChanged);
                //m_Watcher.Renamed += new RenamedEventHandler(OnRenamed);
                m_Watcher.EnableRaisingEvents = true;
            }
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            FileStatus a = new FileStatus();
            a.FileName = e.Name;
            a.FilePath = e.FullPath;
            currentFile = a;
            FilesToArchive.Add(a);

            if (!m_bDirty)
            {
                m_Sb.Remove(0, m_Sb.Length);
                m_Sb.Append(e.FullPath);
                m_Sb.Append(" ");
                m_Sb.Append(e.ChangeType.ToString());
                m_Sb.Append("    ");
                m_Sb.Append(DateTime.Now.ToString());
                m_bDirty = true;
                StartUpload(a);
            }
        }

        private static void MoveFileToArchive(FileStatus a)
        {
            try
            {
                m_bDirty = false;
                if (a.IsUploaded)
                    File.Move(a.FilePath, strArchivePath + "\\" + a.FileName);
                a.IsArchived = true;
            }
            catch (Exception)
            {

            }

        }

        private void StartUpload(FileStatus a)
        {
            a.UploadStartTime = DateTime.Now;
            Aspera.Transfer.Environment.setFaspScpPath("C:\\AsperaTest\\Aspera\\ascp.exe");
            /*
             * Set the local management port to be used by Fasp manager.
             * If not set, Fasp Manager will pick a random port and use it.
             * It is best to not set this port.
             */
            Aspera.Transfer.Environment.setManagementPort(54002);
            FaspManager manager = FaspManager.getInstance();
            MyListener myListener = new MyListener();

            /* Uncomment the following two lines to receive 
             * notifications about transfers not initiated by fasp manager 
             */
            manager.listenForServerSessions(true);
            manager.addListener(myListener);
            LocalFileLocation uploadSource = new LocalFileLocation();
            uploadSource.addPath(a.FilePath);
            RemoteFileLocation uploadDestination = new RemoteFileLocation("hack2.aspera.us", "xfer", "aspera");
            uploadDestination.addPath(".");
            XferParams uploadXferParams = getDefaultXferParams();
            JobOrder uploadOrder = new JobOrder(uploadSource, uploadDestination, uploadXferParams);
            String uploadJobId = manager.startTransfer(uploadOrder, myListener);
            a.IsUploaded = true;
        }

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            if (!m_bDirty)
            {
                m_Sb.Remove(0, m_Sb.Length);
                m_Sb.Append(e.OldFullPath);
                m_Sb.Append(" ");
                m_Sb.Append(e.ChangeType.ToString());
                m_Sb.Append(" ");
                m_Sb.Append("to ");
                m_Sb.Append(e.Name);
                m_Sb.Append("    ");
                m_Sb.Append(DateTime.Now.ToString());
                m_bDirty = true;
                if ((bool)selectFile.IsChecked)
                {
                    m_Watcher.Filter = e.Name;
                    m_Watcher.Path = e.FullPath.Substring(0, e.FullPath.Length - m_Watcher.Filter.Length);
                }
            }
        }


        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            Login.Visibility = Visibility.Collapsed;
            Ldd.Visibility = Visibility.Visible;
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            Nullable<bool> result = dlg.ShowDialog();
            if (result == true)
            {
                strFilePath.Text = dlg.FileName;
            }
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog folderDialog = new FolderBrowserDialog();
            folderDialog.SelectedPath = "C:\\";

            DialogResult result = folderDialog.ShowDialog();
            if (result.ToString() == "OK")
                strFolderPath.Text = folderDialog.SelectedPath;
        }

        private void Label_MouseUp_1(object sender, MouseButtonEventArgs e)
        {
            this.Hide();
        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog folderDialog = new FolderBrowserDialog();
            folderDialog.SelectedPath = "C:\\";

            DialogResult result = folderDialog.ShowDialog();
            if (result.ToString() == "OK")
                strArchiveFolderPath.Text = folderDialog.SelectedPath;
        }

        private void Button_Click_5(object sender, RoutedEventArgs e)
        {
            Login.Visibility = Visibility.Visible;
            Ldd.Visibility = Visibility.Collapsed;
        }


        private static XferParams getDefaultXferParams()
        {
            XferParams p = new XferParams();
            p.tcpPort = 22;
            p.udpPort = 33001;
            p.targetRateKbps = 100000;
            p.minimumRateKbps = 0;
            p.encryption = true;
            p.contentProtectionPassphrase = "aspera";
            p.policy = Policy.FAIR;
            p.cookie = "SampleJob";
            p.token = "SampleToken";
            p.preCalculateJobSize = true;
            p.resumeCheck = Resume.OFF;
            return p;
        }

        // All listener classes must inherit 'FileTransferListener' class.
        public class MyListener : FileTransferListener
        {
            /// <summary>
            /// This is the call back method called by the FaspManager on any event
            /// </summary>
            /// <param name="asperaEvent">The event for callback.</param>
            /// <param name="sessionStats">The Session levels statistics for the Session that triggered this event</param>
            /// <param name="fileStats">Statistics of the last file transferred in this session</param>
            public void fileSessionEvent(TransferEvent asperaEvent, SessionStats sessionStats, FileStats fileStats)
            {
                Console.WriteLine("Job Name: " + sessionStats.Cookie);
                Console.WriteLine("\tJob State: " + sessionStats.State);
                Console.WriteLine("\tTarget Rate: " + sessionStats.TargetRateKbps + "Kbps");
                if (sessionStats.ElapsedUSec > 0)
                    Console.WriteLine("\tAvg Actual Rate: " + (sessionStats.TotalTransferredBytes * 8 * 1000 / sessionStats.ElapsedUSec) + "Kbps");
                Console.WriteLine("\tSession Downloaded: " + (sessionStats.TotalTransferredBytes / 1000) + "KB");
                if (fileStats != null)
                {
                    Console.WriteLine("\tFileName: " + fileStats.name);
                    Console.WriteLine("\tFile Downloaded: " + (fileStats.writtenBytes / 1000) + "KB");
                }
                Console.Write("\n");

                // And stop the fasp manager
                if (asperaEvent == TransferEvent.SESSION_STOP || asperaEvent == TransferEvent.SESSION_ERROR)
                {
                    currentFile.IsUploaded = true;
                    currentFile.UploadEndTime = DateTime.Now;
                    try
                    {
                        if (FaspManager.getInstance().getSessionIDList().Count == 1)
                        {
                            //FaspManager.destroy();
                            MoveFileToArchive(currentFile);

                        }
                    }
                    catch (Exception)
                    {

                    }

                }

                if (asperaEvent == TransferEvent.SESSION_ERROR)
                {
                    Console.WriteLine(sessionStats.ErrorDescription);
                }
            }
        }
    }


}