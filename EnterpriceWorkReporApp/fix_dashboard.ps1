$content = Get-Content -Path "d:\EnterpriceWorkReporApp\Views\Pages\DashboardPage.xaml" -Encoding UTF8

$content = $content -replace 'Content="[^"]* Manage Project Paths"', 'Content="&#x2699; Manage Project Paths"'
$content = $content -replace 'Text="[^"]*0\.00" FontSize="32" FontWeight="Bold"', 'Text="&#x20B9;0.00" FontSize="32" FontWeight="Bold"'
$content = $content -replace 'Content="[^"]*CLOCK IN"', 'Content="&#x25B6;  CLOCK IN"'
$content = $content -replace 'Text="[^"]*Quality Performance \(Weekly\)"', 'Text="&#x2B50; Quality Performance (Weekly)"'
$content = $content -replace 'Text="[^"]*Resume Pipeline \(Recruitment\)"', 'Text="&#x1F4CB; Resume Pipeline (Recruitment)"'
$content = $content -replace 'Text="[^"]*Top 3 Quality \(This Month\)"', 'Text="&#x2B50; Top 3 Quality (This Month)"'
$content = $content -replace 'Text="[^"]*Top 3 Work Count \(This Month\)"', 'Text="&#x1F4CB; Top 3 Work Count (This Month)"'

$content | Set-Content -Path "d:\EnterpriceWorkReporApp\Views\Pages\DashboardPage.xaml" -Encoding UTF8
Write-Output "Fixed remaining garbled characters using regex."
