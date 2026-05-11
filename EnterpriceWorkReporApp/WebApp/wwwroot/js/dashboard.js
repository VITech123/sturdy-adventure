// Dashboard Chart Initialization - ES5 Only (IE11 Safe)
// Uses Chart.js 2.9.4

(function () {
    'use strict';

    // Wait for DOM
    $(document).ready(function () {
        try {
            initAreaChart();
        } catch (e) {
            console.log('Chart initialization failed: ' + e.message);
            var el = document.getElementById('myAreaChart');
            if (el && el.parentNode) {
                el.parentNode.innerHTML = '<p class="text-muted text-center p-4">Chart could not be loaded. Please use a modern browser for full functionality.</p>';
            }
        }
    });

    function initAreaChart() {
        var ctx = document.getElementById('myAreaChart');
        if (!ctx) return;

        var labels = ['Week 1', 'Week 2', 'Week 3', 'Week 4', 'Week 5', 'Week 6', 'Week 7'];
        var billingData = [12400, 15800, 14200, 18900, 21000, 19500, 22300];
        var objectsData = [480, 520, 490, 610, 680, 640, 720];

        new Chart(ctx, {
            type: 'line',
            data: {
                labels: labels,
                datasets: [
                    {
                        label: 'Billing (₹)',
                        data: billingData,
                        backgroundColor: 'rgba(0, 48, 135, 0.08)',
                        borderColor: '#003087',
                        borderWidth: 2,
                        pointBackgroundColor: '#003087',
                        pointBorderColor: '#fff',
                        pointBorderWidth: 2,
                        pointRadius: 4,
                        pointHoverRadius: 6,
                        fill: true,
                        lineTension: 0.3
                    },
                    {
                        label: 'Objects Completed',
                        data: objectsData,
                        backgroundColor: 'rgba(40, 167, 69, 0.08)',
                        borderColor: '#28a745',
                        borderWidth: 2,
                        pointBackgroundColor: '#28a745',
                        pointBorderColor: '#fff',
                        pointBorderWidth: 2,
                        pointRadius: 4,
                        pointHoverRadius: 6,
                        fill: true,
                        lineTension: 0.3,
                        yAxisID: 'y-axis-2'
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                legend: {
                    display: true,
                    position: 'top',
                    labels: {
                        fontFamily: '"Segoe UI", Tahoma, Geneva, Verdana, sans-serif',
                        fontSize: 12,
                        padding: 20
                    }
                },
                scales: {
                    xAxes: [{
                        gridLines: {
                            display: false
                        },
                        ticks: {
                            fontFamily: '"Segoe UI", Tahoma, Geneva, Verdana, sans-serif',
                            fontSize: 12
                        }
                    }],
                    yAxes: [
                        {
                            id: 'y-axis-1',
                            position: 'left',
                            ticks: {
                                beginAtZero: true,
                                fontFamily: '"Segoe UI", Tahoma, Geneva, Verdana, sans-serif',
                                fontSize: 12,
                                callback: function (value) {
                                    return '\u20B9' + value.toLocaleString();
                                }
                            },
                            gridLines: {
                                color: 'rgba(0, 0, 0, 0.05)'
                            }
                        },
                        {
                            id: 'y-axis-2',
                            position: 'right',
                            ticks: {
                                beginAtZero: true,
                                fontFamily: '"Segoe UI", Tahoma, Geneva, Verdana, sans-serif',
                                fontSize: 12
                            },
                            gridLines: {
                                display: false
                            }
                        }
                    ]
                },
                tooltips: {
                    backgroundColor: '#003087',
                    titleFontFamily: '"Segoe UI", Tahoma, Geneva, Verdana, sans-serif',
                    bodyFontFamily: '"Segoe UI", Tahoma, Geneva, Verdana, sans-serif',
                    cornerRadius: 4,
                    callbacks: {
                        label: function (item, data) {
                            var label = data.datasets[item.datasetIndex].label || '';
                            if (item.datasetIndex === 0) {
                                return label + ': \u20B9' + item.yLabel.toLocaleString();
                            }
                            return label + ': ' + item.yLabel;
                        }
                    }
                }
            }
        });
    }
})();
