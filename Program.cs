using Newtonsoft.Json;
using Renci.SshNet;
using Renci.SshNet.Sftp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SSHBot
{
	sealed class Config
	{
		public class Host
		{
			public string Hostname { get; set; }
			public string Username { get; set; }
			public string Password { get; set; }
			public string PrivateKeyFile { get; set; }
			public FileTransfer[] UploadFiles { get; set; }
			public string[] RunCommands { get; set; }
			public FileTransfer[] DownloadFiles { get; set; }
		}

		public class FileTransfer
		{
			public string RemoteFile { get; set; }
			public string LocalFile { get; set; }
		}

		[JsonProperty(Required = Required.Always)]
		public Host[] Hosts { get; set; }
	}

	static class Program
	{
		static Config config;

		static void Main(string[] args)
		{
			var exePath = System.Reflection.Assembly.GetEntryAssembly().Location;
			var configPath = args.Length >= 1 ? args[0] : Path.Combine(Path.GetDirectoryName(exePath), "config.json");
			config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(configPath));

			Console.WriteLine($"SSH Bot");

			foreach (var host in config.Hosts)
			{
				Console.WriteLine($"Connecting to '{host.Hostname}' as '{host.Username}'.");

				var connectionInfo = new ConnectionInfo(host.Hostname, host.Username, host.PrivateKeyFile != null ?
					(AuthenticationMethod)new PrivateKeyAuthenticationMethod(host.Username, new PrivateKeyFile(host.PrivateKeyFile)) :
					(AuthenticationMethod)new PasswordAuthenticationMethod(host.Username, host.Password));

				// Disable ECDSA on platforms that don't support it.
				try { using (var ecdsa = new System.Security.Cryptography.ECDsaCng()) ; }
				catch (NotImplementedException)
				{
					var algsToRemove = connectionInfo.HostKeyAlgorithms.Keys.Where(algName => algName.StartsWith("ecdsa")).ToArray();
					foreach (var algName in algsToRemove) connectionInfo.HostKeyAlgorithms.Remove(algName);
				}

				if (host.UploadFiles != null)
				{
					using (var sftp = new SftpClient(connectionInfo))
					{
						sftp.Connect();

						Console.WriteLine($"Connected for uploading files.");

						foreach (var fileTransfer in host.UploadFiles)
						{
							Console.WriteLine($"Uploading '{fileTransfer.LocalFile}' to '{fileTransfer.RemoteFile}'.");

							using (var file = new FileStream(fileTransfer.LocalFile, FileMode.Open, FileAccess.Read))
								sftp.UploadFile(file, fileTransfer.RemoteFile, true);
						}
					}
				}

				if (host.RunCommands != null)
				{
					using (var ssh = new SshClient(connectionInfo))
					{
						ssh.Connect();

						Console.WriteLine($"Connected for running commands.");

						foreach (var command in host.RunCommands)
						{
							Console.WriteLine($"Running '{command}'.");

							var result = ssh.RunCommand(command);

							Console.Write(result.Result);
							Console.WriteLine($"Exit status = {result.ExitStatus}");
						}
					}
				}

				if (host.DownloadFiles != null)
				{
					using (var sftp = new SftpClient(connectionInfo))
					{
						sftp.Connect();

						Console.WriteLine($"Connected for downloading files.");

						foreach (var fileTransfer in host.DownloadFiles)
						{
							Console.WriteLine($"Downloading '{fileTransfer.RemoteFile}' to '{fileTransfer.LocalFile}'.");

							using (var file = new FileStream(fileTransfer.LocalFile, FileMode.Create, FileAccess.Write))
								sftp.DownloadFile(fileTransfer.RemoteFile, file);
						}
					}
				}
			}
		}
	}
}
