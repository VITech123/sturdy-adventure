$content = Get-Content -Path "d:\EnterpriceWorkReporApp\Views\Pages\DashboardPage.xaml" -Encoding UTF8

# Remove all &#x...; symbols from the file
$content = $content -replace '&#x[A-Fa-f0-9]+;\s*', ''

# Except for TotalBillingText which has a symbol without a space, we just want to remove the symbol
# The above regex handles it because \s* matches zero or more spaces.

$content | Set-Content -Path "d:\EnterpriceWorkReporApp\Views\Pages\DashboardPage.xaml" -Encoding UTF8
Write-Output "Successfully removed symbols from DashboardPage.xaml"
