//
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using NuGet;
using Mono.Options;

class MuGet {
	OptionSet options;

	public bool Verbose;
	public string ApiKey;
	public string SourceUrl = "http://go.microsoft.com/fwlink/?LinkID=206669";
	bool showHelp;
	public List<string> Arguments;

	IPackageRepository repoFactory;
	public IPackageRepository PackageRepository {
		get {
			if (repoFactory == null)
				repoFactory = PackageRepositoryFactory.Default.CreateRepository (new PackageSource (SourceUrl, "feed"));
			return repoFactory;
		}
	}

	IPackage [] packages;
	public IPackage [] Packages {
		get {
			var wait = new ManualResetEvent (false);
			Task.Factory.StartNew (delegate {
				if (packages == null)
					packages = PackageRepository.GetPackages ().ToArray ();
				wait.Set ();
			});
			while (!wait.WaitOne (TimeSpan.FromSeconds (1)))
				Spin ("Loading ");
			Console.WriteLine ();
			
			return packages;
		}
	}

	static char [] spinner = new char [] { '|', '/', '-', '\\' };
	static int spinnerIdx;
	public void Spin (string prefix)
	{
		Console.Write ("{0}{1}\r", prefix, spinner [spinnerIdx]);
		spinnerIdx = (spinnerIdx+1)%spinner.Length;
	}

	public int InstallCommand ()
	{
		if (Arguments.Count < 1){
			Console.WriteLine ("muget: you must specify a package to install");
			return 1;
		}
		var package = Arguments [0];
		var version = Arguments.Count > 1 ? new Version (Arguments [1]) : null;
			
		var pm = new PackageManager (PackageRepository, "packages"){
			Logger = new Logger ()
		};
		pm.InstallPackage (package, version);
		
		return 0;
	}
	
	public int ListCommand ()
	{
		var packages = Packages;
		string terms = Arguments.Count != 0 ? Arguments [0].ToLower () : null;
		bool hasPackages = false;
		
		foreach (var package in packages){
			if (terms != null)
				if (package.Id.ToLower ().IndexOf (terms) == -1)
					continue;
			
			hasPackages = true;
			if (Verbose)
				Console.WriteLine ("{0}\n  Version: {1}\n  {2}", package.Id, package.Version, Fmt ("  ", "Description: " + package.Description));
			else 
				Console.WriteLine (package.GetFullName ());
		}
		if (!hasPackages)
			Console.WriteLine ("muget: no packages");	
		return 0;
	}
	
	public void ShowHelp (bool interactive)
	{
		Console.WriteLine ("Usage is: muget command [OPTIONS]");
		Console.WriteLine ("Commands:");
		Console.WriteLine ("   list [pattern]");
		Console.WriteLine ("   install PACKAGE [VERSION]");
		Console.WriteLine (" delete (rm), install (in), list (ls), pack, publish (pub), push, update (up)");
		Console.WriteLine ("Options:");
		options.WriteOptionDescriptions (Console.Out);
	}

	public MuGet ()
	{
		options = new OptionSet () {
			{ "verbose", "Operate in verbose mode", f => Verbose = true },
			{ "a|apikey=", "Specifies the API key", f => ApiKey = f },
			{ "h|help", "Show this help", f => showHelp = true },
			{ "s|source", "Sets the source repository url", f => SourceUrl = f }
		};
	}

	public void Interactive ()
	{
		var lineEditor = new Mono.Terminal.LineEditor ("muget");
		string s;

		while ((s = lineEditor.Edit ("muget> ", "")) != null){
			Run (s.Split (' '), true);
		}
	}
	
	public int Run (string [] args, bool interactive)
	{
		try {
			Arguments = options.Parse (args);
		} catch (OptionException e){
			if (interactive)
				Console.WriteLine ("Parsing error: {0}", e.Message);
			else
				Console.WriteLine ("Error: {0}\nTry using --help", e.Message);
			return 1;
		}
		if (showHelp){
			ShowHelp (interactive);
			return 0;
		}

		if (Arguments.Count == 0){
			Interactive ();
			return 0;
		}
		
		string command = Arguments [0].ToLower ();
		Arguments = Arguments.Skip (1).ToList ();
		switch (command){
		case "delete": case "rm":
		case "install": case "in":
			return InstallCommand ();
			
		case "list": case "ls":
			return ListCommand ();
			
		case "pack":
			return PackCommand ();
			
		case "publish": case "pub":
		case "update": case "up":

		default:
			Console.Error.WriteLine ("muget: unknown command {0}", command);
			ShowHelp (interactive);
			return 1;
		}

		if (!interactive)
			return 1;

		// Interactive commands
		switch (command){
		case "quit":
			if (interactive)
				Environment.Exit (0);
			break;

		case "help":
			ShowHelp ();
			break;
			
		}
	}
	
	public static int Main (string [] args)
	{
		var muget = new MuGet ();
		return muget.Run (args, false);
	}

	int WindowWidth ()
	{
		try {
			return Console.WindowWidth;
		} catch {
			return 80;
		}
	}
	
	string Fmt (string prefix, string text)
	{
		int wrap = Console.WindowWidth-8;
		var result = new StringBuilder (prefix);
		int col = prefix.Length;
		
		foreach (char c in text){
			if (col > wrap){
				result.Append ("\n");
				result.Append (prefix);
				col = prefix.Length;
			}
			if (col == prefix.Length && c == ' ')
				continue;
			
			if (c == '\n')
				col = 0;
			if (c == '\t')
				col = (col/8+1)*8;
			if (c == '\r')
				continue;
			result.Append (c);
		}
		return result.ToString ();
	}
}

class Logger : ILogger {
	public void Log (MessageLevel level, string format, params object [] args)
	{
		Console.WriteLine (format, args);
	}
}
