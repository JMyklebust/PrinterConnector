param(
    $InputConfig = "$PSScriptRoot\configuration.xml",
    $OutputPath = "$PSScriptRoot"
)
$ConfigFileInfo = Get-ItemProperty $InputConfig
$OutFilename = (Join-Path $OutputPath ($ConfigFileInfo.BaseName + ".toml"))

$xmlDoc = [xml]::new()
$xmlDoc.Load($InputConfig)
$stringBuilder = [System.Text.StringBuilder]::New()


[void]$stringBuilder.AppendLine("# Main settings")

foreach($attrib in $xmlDoc.printerconnector.configuration.Attributes){
    [void]$stringBuilder.AppendLine($attrib.Name +"="+$attrib.Value)
}
[void]$stringBuilder.AppendLine("")
[void]$stringBuilder.AppendLine("# Printer configuration")
foreach($printer in $xmlDoc.printerconnector.printers.printerdef){
    [void]$stringBuilder.AppendLine("[printer.'$($printer.name.Split("\")[-1])']")
    [void]$stringBuilder.AppendLine("name='$($printer.name)'")

    foreach($info in $printer.ChildNodes){
        if($info.name -eq "adgroup"){
            $array = $info.InnerText -split ","
            $array = ($array | ForEach-Object {$psitem.trim()}) -join "','"
            [void]$stringBuilder.AppendLine("adgroup=['$array']")
        }
        elseif($info.name -eq "computers"){
            $array = $info.InnerText -split ","
            $array = ($array | ForEach-Object {$psitem.trim()}) -join "','"
            [void]$stringBuilder.AppendLine("computers=['$array']")
        }
        elseif($info.name -eq "ipaddress"){
            $array = $info.InnerText -split ","
            $array = ($array | ForEach-Object {$psitem.trim()}) -join "','"
            [void]$stringBuilder.AppendLine("ipaddress=['$array']")
        }
        elseif($info.name -eq "setdefaultprinter"){
            [void]$stringBuilder.AppendLine("setdefaultprinter=$($info.InnerText)")
            if($info.HasAttribute("weight")){
                [void]$stringBuilder.AppendLine("defaultprinterweight=$($info.GetAttribute("weight"))")
            }
        }
    }
    [void]$stringBuilder.AppendLine("")
}


$stringBuilder.ToString() | Set-Content -Path $OutFilename -Encoding UTF8
