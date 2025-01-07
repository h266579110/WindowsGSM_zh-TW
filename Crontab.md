## Crontab Managing
Improved Crontab Managing.
Crontabs can now also Execute Windows commands and send Server Console Commands
They can now be configured by adding *.csv files to the server config folder (servers%ServerID%\configs\Crontab)
You can Add multiple lines to that csv file, and also add multiple files. WGSM will try to read all *.csv files in that folder.
Comments can be added by 2 leading slashes "//" as first characters in that line
### File Structure
> CrontabExpression;Type;Command;Arguments

Example Contents for Execute:
> 6 * * *;exec;cmd.exe;/C "C:\Full\Path\To\test.bat"
> 7 * * *;exec;ping.exe;127.0.0.1 /n 8

Example for sending Commands:
> 1 * * * *;ServerConsoleCommand;cheat serverchat this message will occure every hour

Example for additional Restarts besides the Gui defined one:
> 2 * * *;restart

### Notes
Make sure none of the crontabs overlapp too much. Exec programms will only be stopped on the Restart of that server, so make sure the programms do not run continously.

The config Folder is Admin only Protected, as this would allow an easy rights escalation

### Crontab syntax
https://crontab.guru/
