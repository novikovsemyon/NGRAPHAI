$ErrorActionPreference = 'Stop'
$packages = @(Get-ChildItem output -Filter *.msi)
if ($packages.Count -ne 2) { throw 'Expected SingleUser and MultiUser installers.' }
$installer = New-Object -ComObject WindowsInstaller.Installer
foreach ($package in $packages) {
    $database = $installer.OpenDatabase($package.FullName, 0)
    $view = $database.OpenView('SELECT `FileName`, `Component_` FROM `File`')
    $view.Execute()
    $files = @()
    while ($record = $view.Fetch()) {
        $files += [pscustomobject]@{ Name = $record.StringData(1).Split('|')[-1]; Component = $record.StringData(2) }
    }
    $view.Close()
    foreach ($name in @('settings.ini', 'АК_АСКУЭ.ini', 'АК_Газ.ini', 'АК_СКС.ini')) {
        # Seven add-in configurations must carry their own templates, for MultiUser and portable deployments.
        if (@($files | Where-Object Name -eq $name).Count -lt 7) { throw "Missing packaged INI: $name in $($package.Name)" }
    }
    if ($package.Name -like '*SingleUser*') {
        # MSI SQL has limited LIKE support; read the table and identify the four explicit file components instead.
        $view = $database.OpenView('SELECT `Component`, `Attributes` FROM `Component`')
        $view.Execute()
        $preserved = 0
        while ($record = $view.Fetch()) {
            if ($record.StringData(1) -like '*NGraphUserIni*') {
                $attributes = $record.IntegerData(2)
                if (($attributes -band 16) -eq 0 -or ($attributes -band 128) -eq 0) { throw 'User INI must be Permanent and NeverOverwrite.' }
                $preserved++
            }
        }
        $view.Close()
        if ($preserved -ne 4) { throw "Expected four preserved user INI components; found $preserved" }
    }
    Write-Host "PASS: packaged INI defaults and preservation flags in $($package.Name)"
}
