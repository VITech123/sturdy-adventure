$content = Get-Content -Path "d:\EnterpriceWorkReporApp\Views\Pages\DashboardPage.xaml" -Encoding UTF8

$content[35] = '                        <TextBlock Text="&#x1F4CB;" FontSize="28"/>'
$content[43] = '                        <TextBlock Text="&#x1F4C4;" FontSize="28" Foreground="{DynamicResource SuccessBrush}"/>'
$content[51] = '                        <TextBlock Text="&#x1F524;" FontSize="28"/>'
$content[59] = '                        <TextBlock Text="&#x20B9;" FontSize="28" Foreground="{DynamicResource SuccessBrush}"/>'
$content[76] = '                            <TextBlock Text="&#x23F0;" FontSize="24" VerticalAlignment="Center" Margin="0,0,12,0"/>'
$content[84] = '                            <Button x:Name="ClockOutBtn" Content="&#x23F9;  CLOCK OUT" Style="{DynamicResource DangerButton}" Width="140" Height="40" Click="ClockOutBtn_Click"/>'
$content[94] = '                        <TextBlock Text="&#x1F465; Today''s Individual Performance" FontSize="15" FontWeight="SemiBold"'

$content | Set-Content -Path "d:\EnterpriceWorkReporApp\Views\Pages\DashboardPage.xaml" -Encoding UTF8
Write-Output "Successfully replaced by array indices!"
