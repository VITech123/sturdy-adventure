$content = Get-Content -Path "d:\EnterpriceWorkReporApp\Views\Pages\DashboardPage.xaml" -Encoding UTF8

$content = $content -replace 'Text="ðŸ“ "', 'Text="&#x1F4CB;"'
$content = $content -replace 'Text="ðŸ“„"', 'Text="&#x1F4C4;"'
$content = $content -replace 'Text="ðŸ”¤"', 'Text="&#x1F524;"'
$content = $content -replace 'Text="â‚¹"', 'Text="&#x20B9;"'
$content = $content -replace 'Text="â °"', 'Text="&#x23F0;"'
$content = $content -replace 'Content="â ¹  CLOCK OUT"', 'Content="&#x23F9;  CLOCK OUT"'
$content = $content -replace 'Text="ðŸ‘¥ Today''s Individual Performance"', 'Text="&#x1F465; Today''s Individual Performance"'

$content | Set-Content -Path "d:\EnterpriceWorkReporApp\Views\Pages\DashboardPage.xaml" -Encoding UTF8
Write-Output "Fixed all remaining garbled characters."
