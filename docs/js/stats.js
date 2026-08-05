async function loadGitHubStats() {
    try {
        const repo = 'ENZYME-APD/enzyme-grasshopper-plugin';
        
        // 1. Fetch contributors
        const contribRes = await fetch(`https://api.github.com/repos/${repo}/contributors`);
        const contributors = await contribRes.json();
        
        const contribContainer = document.getElementById('dynamic-contributors');
        if (contribContainer && Array.isArray(contributors)) {
            const links = contributors.map(c => 
                `<a href="${c.html_url}" target="_blank" style="color: #fff; text-decoration: none; border-bottom: 1px solid #3f3f46; transition: border-color 0.2s ease;" onmouseover="this.style.borderColor='#2dd4a0'" onmouseout="this.style.borderColor='#3f3f46'">${c.login}</a>`
            );
            contribContainer.innerHTML = links.join(', ');
        }

        // 2. Fetch commits for graph
        const commitsRes = await fetch(`https://api.github.com/repos/${repo}/commits?per_page=100`);
        const commits = await commitsRes.json();
        
        if (Array.isArray(commits)) {
            const daily = {};
            commits.forEach(c => {
                const date = new Date(c.commit.author.date);
                const key = date.toISOString().split('T')[0];
                daily[key] = (daily[key] || 0) + 1;
            });
            
            const sortedKeys = Object.keys(daily).sort();
            const labels = [];
            const data = [];
            
            if (sortedKeys.length > 0) {
                let current = new Date(sortedKeys[0]);
                const end = new Date();
                while (current <= end) {
                    const key = current.toISOString().split('T')[0];
                    const monthNames = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
                    labels.push(monthNames[current.getMonth()] + ' ' + current.getDate());
                    data.push(daily[key] || 0);
                    current.setDate(current.getDate() + 1);
                }
            }

            const ctxChart = document.getElementById('contributionsChart');
            if (ctxChart && typeof Chart !== 'undefined') {
                new Chart(ctxChart, {
                    type: 'line',
                    data: {
                        labels: labels,
                        datasets: [{
                            label: 'Commits',
                            data: data,
                            borderColor: '#2dd4a0',
                            backgroundColor: 'rgba(45, 212, 160, 0.05)',
                            borderWidth: 2,
                            pointBackgroundColor: '#111116',
                            pointBorderColor: '#2dd4a0',
                            pointHoverBackgroundColor: '#2dd4a0',
                            fill: true,
                            tension: 0.4
                        }]
                    },
                    options: {
                        responsive: true,
                        maintainAspectRatio: false,
                        plugins: {
                            legend: { display: false },
                            tooltip: {
                                backgroundColor: '#111116',
                                titleColor: '#fff',
                                bodyColor: '#a1a1aa',
                                borderColor: '#1e1e26',
                                borderWidth: 1
                            }
                        },
                        scales: {
                            x: {
                                grid: { color: 'rgba(255, 255, 255, 0.02)' },
                                ticks: { color: '#a1a1aa', maxTicksLimit: 10 }
                            },
                            y: {
                                beginAtZero: true,
                                grid: { color: 'rgba(255, 255, 255, 0.05)' },
                                ticks: { color: '#a1a1aa', stepSize: 1 }
                            }
                        }
                    }
                });
            }
        }
    } catch (e) {
        console.error("Error loading GitHub stats:", e);
    }
}

document.addEventListener('DOMContentLoaded', loadGitHubStats);
