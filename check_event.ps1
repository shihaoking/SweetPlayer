$asm = [System.Reflection.Assembly]::LoadFrom("C:\Users\Simon\.nuget\packages\libmpv.client\1.0.0\lib\netstandard2.0\LibMpv.Client.dll")
$t = $asm.GetType("LibMpv.Client.MpvEvent")
Write-Output "MpvEvent fields:"
$t.GetFields() | ForEach-Object { Write-Output "  $($_.FieldType.Name) $($_.Name)" }
Write-Output "MpvEvent properties:"
$t.GetProperties() | ForEach-Object { Write-Output "  $($_.PropertyType.Name) $($_.Name)" }
