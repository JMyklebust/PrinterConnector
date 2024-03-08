# This is a sample script on how to configure a scheduled task to launch the PrinterConnector application
# on login and workstation unlock

$TaskName = "PrinterConnector"
# We strictly don't need to specify the working directory if we use absolute paths for the application and config
# But might be a good idea to specify anyway
$PrinterConnectorWorkingDirectory = "$env:ProgramFiles\PrinterConnector\"
# The path to the program, can be an absolute path (reccomended) or a relative path to the working directory
$PrinterConnectorPath = "$env:ProgramFiles\PrinterConnector\PrinterConnector.exe"
# The path for the config file, again can be relative to working dir or an absolute path
# Note that we need "" around the path name in the task scheduler, so in this case we use """<path>""" to escape the inner ""
# Specifying the config is also optional, if we don't specify it, the program will look for 'configuration.xml' in the working directory-
$PrinterConnectorXMLConfig = """$env:ProgramFiles\PrinterConnector\configuration.xml"""

$Action = New-ScheduledTaskAction -Execute $PrinterConnectorPath -Argument $PrinterConnectorXMLConfig -WorkingDirectory $PrinterConnectorWorkingDirectory

# Session change trigger
# We take the long way around to create a "Workstation unlock" trigger since we dont have a PowerShell cmdlet for it.
# https://learn.microsoft.com/en-us/windows/win32/taskschd/sessionstatechangetrigger
# Get the CIM class for the trigger
$SessionStateTriggerClass = Get-cimclass -Namespace root/Microsoft/Windows/TaskScheduler -Class MSFT_TaskSessionStateChangeTrigger
# Create a new instance of the class
$WorkStationUnlockTrigger = $SessionStateTriggerClass | New-CimInstance -ClientOnly
# Make sure trigger is enabled
$WorkStationUnlockTrigger.Enabled = $true
# Set to trigger on workstation unlock
$WorkStationUnlockTrigger.StateChange = 8

# Create an array of triggers
$Triggers = @(
    (New-ScheduledTaskTrigger -AtLogOn)
    $WorkStationUnlockTrigger
)
# Specify some settings
$Settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -DontStopOnIdleEnd
# Set the task to run on the user "Interactive", meaning it should trigger when user logs onto the main/console session
$Principal = New-ScheduledTaskPrincipal -GroupID "S-1-5-4"

# Put it all together and register the task
$Task = New-ScheduledTask -Action $Action -Trigger $Triggers -Principal $Principal -Settings $Settings 
Register-ScheduledTask -TaskName $TaskName -InputObject $Task -TaskPath "\"