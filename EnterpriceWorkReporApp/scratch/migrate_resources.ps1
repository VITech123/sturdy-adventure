# Bulk migrate StaticResource to DynamicResource in XAML files
$Files = Get-ChildItem -Path "d:\EnterpriceWorkReporApp\Views" -Filter "*.xaml" -Recurse

foreach ($File in $Files) {
    Write-Host "Processing $($File.FullName)..."
    $Content = Get-Content $File.FullName -Raw
    
    # Replace StaticResource with DynamicResource, but exclude common converters
    # Using regex to match {StaticResource KeyName}
    # We exclude BoolToVis, InverseBoolToVis, NullToVisibility
    $NewContent = [regex]::Replace($Content, '\{StaticResource (?!BoolToVis|InverseBoolToVis|NullToVisibility)([\w\d_]+)\}', '{DynamicResource $1}')
    
    if ($Content -ne $NewContent) {
        Set-Content -Path $File.FullName -Value $NewContent
        Write-Host "  Updated!" -ForegroundColor Green
    }
}
