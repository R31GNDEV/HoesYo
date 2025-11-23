using Microsoft.Win32;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Security;

public class ChronoVaultActivator
{
    private const string GAME_PATH = @"D:\Games\HoYoPlay\games\Genshin Impact game\GenshinImpact.exe";
    private const double SOUND_THRESHOLD = 0.00001;
    private const int MAX_WAIT_TIME_SECONDS = 60;
    private const int SAMPLE_RATE = 44100;
    private const int CHECK_INTERVAL_MS = 500;
    private const int GAME_LOAD_WAIT_SECONDS = 5;

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern int ShellExecute(IntPtr hwnd, string lpOperation, string lpFile, string lpParameters, string lpDirectory, int nShowCmd);

    [DllImport("shell32.dll")]
    private static extern bool IsUserAnAdmin();

    private const string TARGET_SERVICE_NAME = "HoYoProtect";
    private const string PRIMARY_DRIVER_NAME = "HoYoKProtect.sys";
    private const string SECONDARY_DRIVER_NAME = "HoYoKProtect.sys";

    private static string _activeAdapterName = null;
    private static bool _audioDetected = false;


    private static bool CheckAndRunAsAdmin()
    {
        if (!IsUserAnAdmin())
        {
            Console.WriteLine("Elevating permissions to administrator...");
            string script = Process.GetCurrentProcess().MainModule.FileName;
            ShellExecute(IntPtr.Zero, "runas", script, null, null, 1);
            return false; 
        }
        return true;
    }

    private static void ExecuteServiceNeutering()
    {
        Console.WriteLine("\n Lockdown: Silent Registry Key Modification...");

        string keyPath = $"SYSTEM\\CurrentControlSet\\Services\\{TARGET_SERVICE_NAME}";

        try
        {
            using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            using (RegistryKey serviceKey = baseKey.OpenSubKey(keyPath, true))
            {
                if (serviceKey == null)
                {
                    Console.WriteLine($"[!] Target key '{keyPath}' not found. Service already dead or missing.");
                    return;
                }

                serviceKey.SetValue("Start", 4, RegistryValueKind.DWord);
                Console.WriteLine($"[*] Set '{keyPath}\\Start' to 4 (SERVICE_DISABLED).");

                serviceKey.SetValue("ErrorControl", 0, RegistryValueKind.DWord);
                Console.WriteLine($"[*] Set '{keyPath}\\ErrorControl' to 0 (IGNORE).");
            }
            Console.WriteLine("[+] Registry Injection COMPLETE. Service neutered.");
        }
        catch (SecurityException)
        {
            Console.WriteLine("[!!] CRITICAL: Permission Denied. You are NOT running as proper Administrator.");
            Environment.Exit(1);
        }
        catch (Exception e)
        {
            Console.WriteLine($"[!!] FAILED during Registry operation: {e.Message}");
        }
    }


    private static ManagementObject GetNetworkAdapterObject(string adapterName)
    {
        string query = $"SELECT * FROM Win32_NetworkAdapter WHERE Name = '{adapterName}'";
        ManagementObjectSearcher searcher = new ManagementObjectSearcher(query);
        ManagementObjectCollection collection = searcher.Get();

        foreach (ManagementObject managementObject in collection)
        {
            return managementObject;
        }
        return null;
    }

    private static string GetActiveNetworkAdapterName()
    {
        if (!string.IsNullOrEmpty(_activeAdapterName))
        {
            return _activeAdapterName;
        }

        try
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapter WHERE NetConnectionStatus = 2 AND AdapterTypeID = 0");
            ManagementObjectCollection collection = searcher.Get();

            foreach (ManagementObject managementObject in collection)
            {
                _activeAdapterName = managementObject["Name"].ToString();
                Console.WriteLine($"[*] Active network adapter detected: {_activeAdapterName}");
                return _activeAdapterName;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[!!] WMI Error during adapter search: {ex.Message}");
        }
        Console.WriteLine("[!!] No active network adapter found. You're trying to bypass, but you have no network.");
        return null;
    }

    private static void DisableNetwork(string adapterName)
    {
        Console.WriteLine($"\n[*] Initiating Network Isolation on '{adapterName}' (via WMI)...");
        ManagementObject adapterObject = GetNetworkAdapterObject(adapterName);

        if (adapterObject != null)
        {
            try
            {
                adapterObject.InvokeMethod("Disable", null);
                Console.WriteLine("[*] Internet disabled. Full isolation achieved.");
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!!] FAILED to disable network via WMI. Error: {ex.Message}");
            }
        }
        Console.WriteLine("[!!] Critical WMI failure. Bypass likely failed.");
        Environment.Exit(1);
    }

    private static void RestoreNetwork(string adapterName)
    {
        Console.WriteLine("\n?? Restoring network adapter (via WMI)...");
        ManagementObject adapterObject = GetNetworkAdapterObject(adapterName);

        if (adapterObject != null)
        {
            try
            {
                adapterObject.InvokeMethod("Enable", null);
                Console.WriteLine("[*] Internet re-enabled. Mission accomplished.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!!] FAILED to re-enable network. Manual fix required. Error: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("[!!] Adapter object lost. Cannot re-enable network.");
        }
    }


    private static void DetectDefaultSpeakerLoopback()
    {
        Console.WriteLine("\n🎧 Initializing audio loopback detection (NAudio)...");

        try
        {
            MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
            MMDevice speaker = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
            Console.WriteLine($"Default output device: {speaker.FriendlyName}");

            using (var capture = new WasapiLoopbackCapture(speaker))
            {
                int bytesPerSample = capture.WaveFormat.BitsPerSample / 8;
                int channels = capture.WaveFormat.Channels;

                capture.DataAvailable += (s, a) =>
                {
                    long totalSquare = 0;
                    for (int i = 0; i < a.BytesRecorded; i += bytesPerSample * channels)
                    {
                        short sample = BitConverter.ToInt16(a.Buffer, i);
                        totalSquare += (long)sample * sample;
                    }

                    double rms = Math.Sqrt((double)totalSquare / (a.BytesRecorded / bytesPerSample));
                    double volume = rms / 32768.0;

                    if (volume > SOUND_THRESHOLD)
                    {
                        Console.WriteLine($"\n[***] Audio signal detected! Volume: {volume:F8} > {SOUND_THRESHOLD:F8}");
                        _audioDetected = true;
                        capture.StopRecording();
                    }
                };

                capture.StartRecording();

                Console.WriteLine($"[*] Monitoring loopback of '{speaker.FriendlyName}' (Max Wait: {MAX_WAIT_TIME_SECONDS}s)...");

                int totalTimeWaitedMs = 0;
                while (capture.CaptureState == CaptureState.Capturing && totalTimeWaitedMs < (MAX_WAIT_TIME_SECONDS * 1000) && !_audioDetected)
                {
                    Thread.Sleep(CHECK_INTERVAL_MS);
                    totalTimeWaitedMs += CHECK_INTERVAL_MS;
                    if ((totalTimeWaitedMs / CHECK_INTERVAL_MS) % 5 == 0)
                    {
                        Console.Write(".");
                    }
                }
            }

            if (!_audioDetected)
            {
                Console.WriteLine($"\n[!!] Audio cue detection timed out. Proceeding to {GAME_LOAD_WAIT_SECONDS}s failsafe wait.");
                Thread.Sleep(GAME_LOAD_WAIT_SECONDS * 1000);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n[!!] Audio detection failed critically: {ex.Message}. Proceeding to {GAME_LOAD_WAIT_SECONDS}s failsafe wait.");
            Thread.Sleep(GAME_LOAD_WAIT_SECONDS * 1000);
        }
    }


    public static void Main(string[] args)
    {
        if (!CheckAndRunAsAdmin())
        {
            return;
        }

        Console.WriteLine("\n[***] (C# v2.0 - WMI Stealth) [***]");

        ExecuteServiceNeutering();

        string adapterName = GetActiveNetworkAdapterName();
        if (string.IsNullOrEmpty(adapterName))
        {
            Console.WriteLine("[!!] Aborting: Cannot perform network isolation.");
            Environment.Exit(1);
        }
        DisableNetwork(adapterName);

        Console.WriteLine("\n?? Launching Game Process...");
        try
        {
            Process.Start(GAME_PATH);
            Console.WriteLine("[*] Game launched. Auth check is stalled.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[!!] CRITICAL: Failed to launch game at {GAME_PATH}. Error: {ex.Message}");
            RestoreNetwork(adapterName);
            Environment.Exit(1);
        }

        Console.WriteLine("\n⏳ **Mock-Auth Window**: Waiting for audio cue or failsafe timer...");

        DetectDefaultSpeakerLoopback();

        Console.WriteLine("\n[*] Synchronization complete. Sealing the deception.");

        RestoreNetwork(adapterName);

        Console.WriteLine("\n✅ The game process is now running, blinded, and connected. Zeta victory.");
        Console.WriteLine("[!] Script completed. The game process remains running.");
    }
}