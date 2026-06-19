# File Monitor Service (Windows Service)

An enterprise-grade, high-performance Windows Service built with **C#** and **.NET Framework**. The service continuously and efficiently monitors a dedicated directory for incoming files of any extension, safely isolates them, applies a unique cryptographic naming convention (`Guid`), routes them to a destination directory, and dispatches real-time granular email alerts asynchronously.

---

## 🚀 Core Features & Architecture

* **High-Performance Monitoring:** Leverages a production-optimized `FileSystemWatcher` to capture file system events instantaneously.
* **Thread-Safe Debouncing:** Utilizes a thread-safe `ConcurrentDictionary` mechanism to effectively suppress duplicate or redundant Windows file system events.
* **Concurrency & Non-Blocking I/O:** Fully asynchronous architecture leveraging `Task` and `await` paradigms to keep the main service thread unblocked during heavy load or email dispatching.
* **Safe File Access (`WaitForFileUnlock`):** Implements an automated polling retry mechanism to guarantee that big or transferring files are fully written and unlocked by the operating system before processing begins.
* **Self-Healing Infrastructure:** On service initialization (inside the constructor right after `InitializeComponent()`), the service scans for all dependent directories and log files on Drive `D:`. If any are missing, it auto-generates them instantly.
* **Robust Enterprise Logging:** Transparently logs every atomic lifecycle action (Service Start/Stop, File Detection, File Moving, Email Dispatch Success, and Detailed Exception Stack Traces) into a local rolling text file on Drive `D:`.

---

## 🛠️ Prerequisites & References

To compile and execute this project, ensure you have:
* **IDE:** Visual Studio 2022
* **Framework:** .NET Framework
* **Required Assembly References:**
  * `System.Configuration` (For accessing `App.config` properties)
  * `System.Configuration.Install` (For linking the `ProjectInstaller`)

---

## ⚙️ Configuration Setup (`App.config`)

The service relies entirely on externalized settings to remain dynamic. All monitored paths, routing destinations, and logging structures are targeted on the **D:** drive. Ensure your `App.config` contains the following structure before building:

```
  <appSettings>
    <add key="SourceFolder" value="D:\Files Monitoring\Source" />
    <add key="BaseFolder" value="D:\Files Monitoring" />
    <add key="DestinationFolder" value="D:\Files Monitoring\Destination" />
    <add key="LogFolder" value="D:\Files Monitoring\Logs" />
    <add key="LogFileName" value="Logs.txt" />
  </appSettings>
```

## 📧 Code Customization Required

Before building the application, navigate to the SendEmailAlertAsync method and customize your SMTP credentials and mail variables.

Inside SendEmailAlertAsync, the following standard .NET networking classes are utilized to achieve secure async mailing:

- ```SmtpClient``` - Handles the secure SMTP transaction over port 587.

- ```MailMessage``` - Encapsulates the envelope, original/new path schemas, and seconds-precise timestamps.

- ```MailAddress``` - Validates structural correctness of sender and receiver endpoints.

- ```NetworkCredential``` - Securely injects credentials into the mail transaction wrapper.

Ensure you supply:

1. Sender Email Address

2. Google App Password (16-character token generated from Google Security settings)

3. Recipient Email Address

The dispatched email will explicitly document the Full Initial Path, Full Destination Path, and the exact timestamp structured down to the second (yyyy-MM-dd HH:mm:ss).

## 📦 Compilation & Installation Guide

Follow these sequential steps to compile, register, and initiate the service inside Windows:

Step 1: Build the Project

1. Open the project inside Visual Studio 2022.

2. Select your build configuration (e.g., ```Release``` or ```Debug```).

3. Right-click the Solution and select Build Solution. This creates the executable inside your target folder (e.g., ```bin\Release\YourServiceName.exe```).

Step 2: Open Command Prompt as Administrator

- Press the ```Windows Key```, type ```cmd```, right-click Command Prompt, and select Run as Administrator.

Step 3: Install the Service Using ```InstallerUtil.exe```

Depending on your system architecture and your targeted framework version, Execute the installation using the absolute path of the Microsoft Native Installer Utility, After you finish building, copy all the files in the Debug or Release folder to another folder you create on your ```C``` or ```D``` drive and name it ```MyServices```, because it's best to have a short path for the installed ```.exe``` file..

- For 64-Bit (x64) Systems:

```C:\Windows\Microsoft.NET\Framework64\v4.0.30319\InstallerUtil.exe "D:\MyServices\YourServiceName.exe"```

- For 32-Bit (x86) Systems:

```C:\Windows\Microsoft.NET\Framework\v4.0.30319\InstallerUtil.exe "D:\MyServices\YourServiceName.exe"```

Step 4: Verify and Start the Service via ```services.msc```

1. Press ```Win + R```, type ```services.msc```, and press ```Enter```.

2. Scroll through the services list to find your registered service name.

3. Right-click the service name and click Start.

4. (Optional) Right-click, go to Properties, and ensure the Startup type is set to Automatic so it remains active upon server reboots.

## 🧹 Uninstallation Guide

Should you need to remove the service from your local machine, execute the utility tool with the ```/u``` flag via Administrator CMD:

- For 64-Bit Systems:

```C:\Windows\Microsoft.NET\Framework64\v4.0.30319\InstallerUtil.exe /u "D:\MyServices\YourServiceName.exe"```