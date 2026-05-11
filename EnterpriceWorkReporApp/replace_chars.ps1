$content = Get-Content -Path "d:\EnterpriceWorkReporApp\Views\Pages\DashboardPage.xaml" -Encoding UTF8 -Raw

$replacements = @{
    "âš™" = "&#x2699;"
    "ðŸ“ " = "&#x1F4CB;"
    "ðŸ“„" = "&#x1F4C4;"
    "ðŸ”¤" = "&#x1F524;"
    "â‚¹" = "&#x20B9;"
    "â °" = "&#x23F0;"
    "â–¶" = "&#x25B6;"
    "â ¹" = "&#x23F9;"
    "ðŸ‘¥" = "&#x1F465;"
    "ðŸ“ˆ" = "&#x1F4C8;"
    "ðŸ“Š" = "&#x1F4CA;"
    "ðŸ“…" = "&#x1F4C5;"
    "â­ " = "&#x2B50;"
    "ðŸ’¬" = "&#x1F4AC;"
}

foreach ($key in $replacements.Keys) {
    $content = $content.Replace($key, $replacements[$key])
}

Set-Content -Path "d:\EnterpriceWorkReporApp\Views\Pages\DashboardPage.xaml" -Value $content -Encoding UTF8
Write-Output "Successfully replaced garbled characters."
