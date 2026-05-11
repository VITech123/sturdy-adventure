$content = Get-Content "d:\EnterpriceWorkReporApp\Views\Pages\DashboardPage.xaml" -Raw
$insertion = @"
            <!-- Resume Pipeline Section (Admin Only) -->
            <Grid x:Name="ResumePipelineCard" Margin="0,0,0,24" Visibility="Collapsed">
                <Border Style="{DynamicResource CardBorder}">
                    <StackPanel>
                        <TextBlock Text="📁 Resume Pipeline (Recruitment)" FontSize="15" FontWeight="SemiBold"
                                   Foreground="{DynamicResource TextPrimaryBrush}" Margin="0,0,0,16"/>
                        <UniformGrid Columns="4">
                            <StackPanel Margin="0,0,12,0" HorizontalAlignment="Center">
                                <TextBlock x:Name="ResumesPendingText" Text="0" FontSize="24" FontWeight="Bold" Foreground="#92400E" HorizontalAlignment="Center"/>
                                <TextBlock Text="Pending" Style="{DynamicResource FieldLabel}" HorizontalAlignment="Center"/>
                            </StackPanel>
                            <StackPanel Margin="6,0,6,0" HorizontalAlignment="Center">
                                <TextBlock x:Name="ResumesOngoingText" Text="0" FontSize="24" FontWeight="Bold" Foreground="#1E40AF" HorizontalAlignment="Center"/>
                                <TextBlock Text="Ongoing" Style="{DynamicResource FieldLabel}" HorizontalAlignment="Center"/>
                            </StackPanel>
                            <StackPanel Margin="6,0,6,0" HorizontalAlignment="Center">
                                <TextBlock x:Name="ResumesRejectedText" Text="0" FontSize="24" FontWeight="Bold" Foreground="#991B1B" HorizontalAlignment="Center"/>
                                <TextBlock Text="Rejected" Style="{DynamicResource FieldLabel}" HorizontalAlignment="Center"/>
                            </StackPanel>
                            <StackPanel Margin="12,0,0,0" HorizontalAlignment="Center">
                                <TextBlock x:Name="ResumesRelievedText" Text="0" FontSize="24" FontWeight="Bold" Foreground="#065F46" HorizontalAlignment="Center"/>
                                <TextBlock Text="Relieved" Style="{DynamicResource FieldLabel}" HorizontalAlignment="Center"/>
                            </StackPanel>
                        </UniformGrid>
                    </StackPanel>
                </Border>
            </Grid>

"@

$newContent = $content -replace '<!-- Mini Leaderboards Row -->', ($insertion + '            <!-- Mini Leaderboards Row -->')
Set-Content "d:\EnterpriceWorkReporApp\Views\Pages\DashboardPage.xaml" $newContent -Encoding UTF8
