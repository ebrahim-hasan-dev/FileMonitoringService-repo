using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;



namespace FileMonitoringService
{
    public partial class FilesMonitoring : ServiceBase
    {
        FileSystemWatcher _Watcher;

        string _SourceFolder;
        string _DestinationFolder;
        string _LogFolder;
        string _BaseFolder;
        string _LogFileName;
        string _LogFilePath;

        // ذاكرة مؤقتة لمنع تكرار الأحداث في نفس الوقت
        static ConcurrentDictionary<string, DateTime> _ProcessedFiles = new ConcurrentDictionary<string, DateTime>();
        static TimeSpan _DebounceTime = TimeSpan.FromSeconds(2);

        public FilesMonitoring()
        {
            InitializeComponent();
            
            _SourceFolder = ConfigurationManager.AppSettings["SourceFolder"];
            _DestinationFolder = ConfigurationManager.AppSettings["DestinationFolder"];
            _LogFolder = ConfigurationManager.AppSettings["LogFolder"];
            _BaseFolder = ConfigurationManager.AppSettings["BaseFolder"];
            _LogFileName = ConfigurationManager.AppSettings["LogFileName"];

            try
            {
                if (string.IsNullOrWhiteSpace(_SourceFolder) || string.IsNullOrWhiteSpace(_DestinationFolder) || string.IsNullOrWhiteSpace(_LogFolder)
                    || string.IsNullOrWhiteSpace(_LogFileName) || string.IsNullOrWhiteSpace(_BaseFolder))
                {
                    throw new ConfigurationErrorsException("Base Folder, Source Folder, Destination Folder, Log Folder or Log File Name not specified in the configuration file");
                }
            }
            catch (Exception ex) 
            {
                WriteLog(ex.Message);

                _SourceFolder = "D:\\Files Monitoring\\Source";
                _DestinationFolder = "D:\\Files Monitoring\\Destination";
                _LogFolder = "D:\\Files Monitoring\\Logs";
                _BaseFolder = "D:\\Files Monitoring";
                _LogFileName = "Logs.txt";
            }

            if (!Directory.Exists(_BaseFolder))
            {
                Directory.CreateDirectory(_BaseFolder);
            }

            if (!Directory.Exists(_SourceFolder))
            {
                Directory.CreateDirectory(_SourceFolder);
            }

            if (!Directory.Exists(_DestinationFolder))
            {
                Directory.CreateDirectory(_DestinationFolder);
            }

            if (!Directory.Exists(_LogFolder))
            {
                Directory.CreateDirectory(_LogFolder);
            }

            _LogFilePath = Path.Combine(_LogFolder, _LogFileName);

            if (!File.Exists(_LogFilePath))
            {
                File.Create(_LogFilePath)?.Close();
            }
        }


        protected override void OnStart(string[] args)
        {
            _Watcher = new FileSystemWatcher(_SourceFolder, "*.*");
            
            // نركز على أهم الفلاتر لتقليل التكرار من البداية
            _Watcher.NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.FileName;

            _Watcher.Created += OnFileCreated;
            _Watcher.EnableRaisingEvents = true;
            _Watcher.IncludeSubdirectories = true;
            _Watcher.InternalBufferSize = 16384;

            WriteLog("Service Running Successfully..");
        }

        string GetNewFilePath(string FullPath)
        {
            string NewFilePath = "";

            try
            {
                NewFilePath = Path.Combine(_DestinationFolder, Guid.NewGuid().ToString() + Path.GetExtension(FullPath));
            }
            catch (Exception ex)
            {
                WriteLog($"An error occurred due to: {ex.Message}");
            }

            return NewFilePath;
        }

        void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            DateTime now = DateTime.Now;
            
            // منع التكرار (Debounce)
            if (_ProcessedFiles.TryGetValue(e.FullPath, out DateTime lastProcessedTime))
            {
                if (now - lastProcessedTime < _DebounceTime)
                {
                    return; // تجاهل التكرار السريع لنفس الملف
                }
            }
            
            _ProcessedFiles[e.FullPath] = now;

            // الانتظار حتى يكتمل تحميل/نسخ الملف تماماً
            if (WaitForFileUnlock(e.FullPath))
            {
                WriteLog($"Alert: A new file has been discovered [{e.FullPath}]. Email is being sent..");

                string NewFilePath = GetNewFilePath(e.FullPath);

                // إرسال الإيميل فوراً
                SendEmailAlertAsync(e.Name, e.FullPath, NewFilePath);

                // نطلق مهمة تعمل في الخلفية بشكل منفصل تماماً (دون تعطيل الكود الرئيسي)
                Task.Run(async () =>
                {
                    // انتظر 10 ثوانٍ كاملة لضمان صد كل الأحداث المكررة للملف
                    await Task.Delay(TimeSpan.FromSeconds(10));

                    // الآن، احذف الملف من القاموس بأمان لتفريغ الذاكرة (RAM)
                    // نستخدم out _ لأننا لا نحتاج للقيمة المحذوفة في شيء (Discard)
                    if (_ProcessedFiles.TryRemove(e.FullPath, out _))
                    {
                        WriteLog($"The file was successfully deleted from the concurrent dictionary memory [{e.FullPath}]");
                    }
                });

                try
                {
                    File.Move(e.FullPath, NewFilePath);

                    WriteLog($"The file was transferred successfully with a new name from [{e.FullPath}] to [{NewFilePath}]");
                }
                catch(Exception ex)
                {
                    WriteLog($"File transfer failed [{e.FullPath}] due to: {ex.Message}");
                }
            }
        }

        async void SendEmailAlertAsync(string FileName, string FilePath, string NewFilePath)
        {
            SmtpClient Smtp = null;
            MailMessage Message = null;

            try
            {
                // إعدادات البريد (المرسل والمستقبل)
                MailAddress FromAddress = new MailAddress("YourGmail@gmail.com", "File Monitoring Service");
                MailAddress ToAddress = new MailAddress("GmailSendTo@gmail.com");
                
                // تذكر: هنا تضع الـ App Password المكون من 16 حرفاً وليس باسووردك العادي
                string FromPassword = "****************";

                string Subject = $"Warning: A new file has been added to the folder! 🔔";

                string Body = $"Hello,\n\nThe system has detected a new file:\n\n" +
                              $"File name: {FileName}\n" +
                              $"Full path: {FilePath}\n" +
                              $"Monitoring time: {DateTime.Now:yyyy-MM-dd hh:mm:ss}\n\n" +
                              $"This file will be moved to this path with a new name [{NewFilePath}].\n\n" +
                              $"This message was sent automatically by Windows Service.";


                // إعداد خادم الـ SMTP الخاص بـ Gmail
                Smtp = new SmtpClient
                {
                    Host = "smtp.gmail.com",
                    Port = 587,
                    EnableSsl = true, // تشفير الاتصال لحماية البيانات
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(FromAddress.Address, FromPassword)
                };

                Message = new MailMessage(FromAddress, ToAddress) { Subject = Subject, Body = Body, Priority = MailPriority.High };

                await Smtp.SendMailAsync(Message);

                WriteLog($"The notification email was successfully sent for the file: [{FilePath}]");
            }
            catch (Exception ex)
            {
                // في حال حدث خطأ في الإنترنت أو الباسوورد، نسجله في الـ Log دون أن تتوقف الخدمة
                WriteLog($"Email failed to send because: {ex.Message}");
            }
            finally
            {
                Message?.Dispose();
                Smtp?.Dispose();
            }
        }

        bool WaitForFileUnlock(string FilePath)
        {
            FileStream Stream = null;
            byte MaxAttempts = 10;

            for (byte i = 0; i < MaxAttempts; i++)
            {
                try
                {
                    Stream = File.Open(FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

                    return true;
                }
                catch (Exception ex)
                {
                    WriteLog($"An error occurred due to: {ex.Message}");

                    Thread.Sleep(500); // انتظر نصف ثانية وأعد المحاولة
                }
                finally
                {
                    Stream?.Close();
                }
            }

            return false;
        }

        protected override void OnStop()
        {
            if (_Watcher != null)
            {
                _Watcher.EnableRaisingEvents = false;
                _Watcher.Dispose();
            }

            WriteLog("Service Stopped");
        }

        void WriteLog(string Message)
        {
            DateTime now = DateTime.Now;

            try
            {
                File.AppendAllText(_LogFilePath, $"[{now.ToString("yyyy-MM-dd hh:mm:ss")}] {Message}.\n");
            }
            catch
            {

            }
        }




        public void DebuggingConsoleMode()
        {
            OnStart(null);

            Console.WriteLine("Press any key to stop service...");
            Console.ReadKey();

            OnStop();

            //FileSystemEventArgs e = new FileSystemEventArgs(WatcherChangeTypes.Created, _SourceFolder, "New Text Document.txt");
            //OnFileCreated(null, e);

        }
    }
}