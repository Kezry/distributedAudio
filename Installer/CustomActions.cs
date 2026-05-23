using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using Microsoft.Deployment.WindowsInstaller;

namespace DistributedAudio.Installer.CustomActions
{
    /// <summary>
    /// WiX 自定义操作
    /// 处理驱动安装和服务配置
    /// </summary>
    public class CustomActions
    {
        /// <summary>
        /// 安装虚拟声卡驱动
        /// </summary>
        [CustomAction]
        public static ActionResult InstallDriverCA(Session session)
        {
            try
            {
                session.Log("Begin InstallDriverCA");

                // 检查管理员权限
                if (!IsAdministrator())
                {
                    session.Log("ERROR: Not running as administrator");
                    return ActionResult.Failure;
                }

                // 获取驱动文件路径
                string driverPath = session.CustomActionData["DRIVERPATH"];
                string infFile = Path.Combine(driverPath, "distributedaudio.inf");

                if (!File.Exists(infFile))
                {
                    session.Log($"ERROR: Driver INF not found: {infFile}");
                    return ActionResult.Failure;
                }

                // 使用pnputil安装驱动
                bool success = InstallDriverWithPnputil(infFile, session);

                if (success)
                {
                    session.Log("Driver installed successfully");
                    return ActionResult.Success;
                }
                else
                {
                    session.Log("ERROR: Driver installation failed");
                    return ActionResult.Failure;
                }
            }
            catch (Exception ex)
            {
                session.Log($"ERROR in InstallDriverCA: {ex.Message}");
                return ActionResult.Failure;
            }
        }

        /// <summary>
        /// 卸载虚拟声卡驱动
        /// </summary>
        [CustomAction]
        public static ActionResult UninstallDriverCA(Session session)
        {
            try
            {
                session.Log("Begin UninstallDriverCA");

                // 使用pnputil卸载驱动
                bool success = UninstallDriverWithPnputil(session);

                if (success)
                {
                    session.Log("Driver uninstalled successfully");
                    return ActionResult.Success;
                }
                else
                {
                    session.Log("WARNING: Driver uninstallation had issues");
                    return ActionResult.Success; // 不阻塞卸载
                }
            }
            catch (Exception ex)
            {
                session.Log($"WARNING in UninstallDriverCA: {ex.Message}");
                return ActionResult.Success; // 不阻塞卸载
            }
        }

        /// <summary>
        /// 配置Windows防火墙
        /// </summary>
        [CustomAction]
        public static ActionResult ConfigureFirewallCA(Session session)
        {
            try
            {
                session.Log("Begin ConfigureFirewallCA");

                string exePath = session.CustomActionData["EXEPATH"];

                // 添加防火墙规则
                using (Process process = new Process())
                {
                    process.StartInfo.FileName = "netsh";
                    process.StartInfo.Arguments = $"advfirewall firewall add rule name=\"Distributed Audio\" dir=in action=allow program=\"{exePath}\"";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.Start();
                    process.WaitForExit();
                }

                // 添加UDP端口规则
                foreach (int port in new[] { 5004, 5005, 5006 })
                {
                    using (Process process = new Process())
                    {
                        process.StartInfo.FileName = "netsh";
                        process.StartInfo.Arguments = $"advfirewall firewall add rule name=\"Distributed Audio UDP {port}\" dir=in action=allow protocol=UDP localport={port}";
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.CreateNoWindow = true;
                        process.Start();
                        process.WaitForExit();
                    }
                }

                session.Log("Firewall configured successfully");
                return ActionResult.Success;
            }
            catch (Exception ex)
            {
                session.Log($"WARNING in ConfigureFirewallCA: {ex.Message}");
                return ActionResult.Success;
            }
        }

        /// <summary>
        /// 启动音频捕获服务
        /// </summary>
        [CustomAction]
        public static ActionResult StartAudioServiceCA(Session session)
        {
            try
            {
                session.Log("Begin StartAudioServiceCA");

                using (Process process = new Process())
                {
                    process.StartInfo.FileName = "sc.exe";
                    process.StartInfo.Arguments = "start DistributedAudioService";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.Start();
                    process.WaitForExit();
                }

                session.Log("Audio service started successfully");
                return ActionResult.Success;
            }
            catch (Exception ex)
            {
                session.Log($"WARNING in StartAudioServiceCA: {ex.Message}");
                return ActionResult.Success;
            }
        }

        // 辅助方法

        private static bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        private static bool InstallDriverWithPnputil(string infPath, Session session)
        {
            try
            {
                // 尝试使用pnputil安装
                using (Process process = new Process())
                {
                    process.StartInfo.FileName = "pnputil";
                    process.StartInfo.Arguments = $"/add-driver \"{infPath}\" /install";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;

                    process.Start();
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    session.Log($"pnputil output: {output}");
                    if (!string.IsNullOrEmpty(error))
                    {
                        session.Log($"pnputil error: {error}");
                    }

                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                session.Log($"Exception in InstallDriverWithPnputil: {ex.Message}");
                return false;
            }
        }

        private static bool UninstallDriverWithPnputil(Session session)
        {
            try
            {
                // 获取已安装的驱动
                using (Process process = new Process())
                {
                    process.StartInfo.FileName = "pnputil";
                    process.StartInfo.Arguments = "/enum-drivers";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.Start();

                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    // 查找distributedaudio驱动
                    string publishedName = null;
                    string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (lines[i].Contains("distributedaudio.inf"))
                        {
                            // 下一行应该包含Published Name
                            if (i + 1 < lines.Length)
                            {
                                var match = System.Text.RegularExpressions.Regex.Match(lines[i + 1], @"Published Name:\s*(.+)");
                                if (match.Success)
                                {
                                    publishedName = match.Groups[1].Value.Trim();
                                    break;
                                }
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(publishedName))
                    {
                        // 卸载驱动
                        using (Process uninstallProcess = new Process())
                        {
                            uninstallProcess.StartInfo.FileName = "pnputil";
                            uninstallProcess.StartInfo.Arguments = $"/delete-driver \"{publishedName}\" /uninstall";
                            uninstallProcess.StartInfo.UseShellExecute = false;
                            uninstallProcess.StartInfo.CreateNoWindow = true;
                            uninstallProcess.Start();
                            uninstallProcess.WaitForExit();

                            session.Log($"Driver uninstalled: {publishedName}");
                            return uninstallProcess.ExitCode == 0;
                        }
                    }
                }

                return true; // 没找到驱动也算成功
            }
            catch (Exception ex)
            {
                session.Log($"Exception in UninstallDriverWithPnputil: {ex.Message}");
                return false;
            }
        }
    }
}
