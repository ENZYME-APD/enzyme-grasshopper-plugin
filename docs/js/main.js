document.addEventListener('DOMContentLoaded', () => {
  const sections = document.querySelectorAll('section');
  const navLinks = document.querySelectorAll('.nav-links a[href^="#"]');

  if (sections.length > 0 && navLinks.length > 0) {
    const observerOptions = {
      root: null,
      rootMargin: '0px',
      threshold: 0.3
    };

    const observer = new IntersectionObserver((entries) => {
      entries.forEach(entry => {
        if (entry.isIntersecting) {
          navLinks.forEach(link => {
            link.classList.remove('active');
            if (link.getAttribute('href') === `#${entry.target.id}`) {
              link.classList.add('active');
            }
          });
        }
      });
    }, observerOptions);

    sections.forEach(section => {
      observer.observe(section);
    });
  }

  // --- Hero Canvas Dot Grid ---
  const canvas = document.getElementById('hero-canvas');
  if (canvas) {
    const ctx = canvas.getContext('2d');
    let width, height;
    
    const modes = ['dots', 'rects', 'pluses', 'rects_v'];
    
    let history = [];
    try {
      history = JSON.parse(localStorage.getItem('bgHistory')) || [];
    } catch(e) {}
    
    let currentMode;
    do {
      currentMode = modes[Math.floor(Math.random() * modes.length)];
    } while (history.length >= 2 && history[0] === currentMode && history[1] === currentMode);
    
    history.unshift(currentMode);
    if (history.length > 2) history.length = 2;
    try {
      localStorage.setItem('bgHistory', JSON.stringify(history));
    } catch(e) {}
    
    let spacingX = 16;
    let spacingY = 16;
    let falloff = 200;
    let baseSize, maxSize;
    
    if (currentMode === 'pluses') {
      baseSize = 1.5;
      maxSize = 2.25; 
    } else if (currentMode === 'rects') {
      baseSize = 4;
      maxSize = 10;
    } else if (currentMode === 'rects_v') {
      baseSize = 2.5; 
      maxSize = 15;
      spacingY = 26; // 24px height + 2px margin to prevent overlap
    } else { // dots
      baseSize = 1.5;
      maxSize = 2.25;
    }
    
    let mouse = { x: -1000, y: -1000 };
    
    function resize() {
      width = canvas.parentElement.offsetWidth;
      height = canvas.parentElement.offsetHeight;
      const dpr = window.devicePixelRatio || 1;
      canvas.width = width * dpr;
      canvas.height = height * dpr;
      ctx.scale(dpr, dpr);
    }
    
    window.addEventListener('resize', resize);
    resize();
    
    const hero = document.querySelector('.hero');
    if (hero) {
      hero.addEventListener('mousemove', (e) => {
        const rect = canvas.getBoundingClientRect();
        mouse.x = e.clientX - rect.left;
        mouse.y = e.clientY - rect.top;
      });
      
      hero.addEventListener('mouseleave', () => {
        mouse.x = -1000;
        mouse.y = -1000;
      });
    }
    
    function draw() {
      ctx.clearRect(0, 0, width, height);
      const time = Date.now() / 1000;
      
      const points = [];
      
      for (let gridX = spacingX / 2; gridX < width + spacingX; gridX += spacingX) {
        let col = [];
        for (let gridY = spacingY / 2; gridY < height + spacingY; gridY += spacingY) {
          let x = gridX;
          let y = gridY;
          
          if (currentMode === 'pluses') {
            const offsetX = Math.sin(gridX * 0.015 + time * 1.2) * (spacingX * 0.25);
            const offsetY = Math.cos(gridY * 0.015 + time * 0.9) * (spacingY * 0.25);
            x += offsetX;
            y += offsetY;
          }
          
          const dx = mouse.x - x;
          const dy = mouse.y - y;
          const dist = Math.sqrt(dx * dx + dy * dy);
          
          let s = baseSize;
          let opacity = 0.20; 
          
          if (dist < falloff) {
            const factor = 1 - (dist / falloff);
            const ease = 1 - Math.pow(1 - factor, 3);
            s = baseSize + (maxSize - baseSize) * ease;
            opacity = 0.20 + (0.20 * ease); 
          }
          
          if (currentMode === 'pluses') {
            const wave = Math.sin(x * 0.03 + time * 2.0) + Math.cos(y * 0.03 - time * 1.5);
            opacity += wave * 0.10; 
            if (opacity < 0.10) opacity = 0.10; 
            if (opacity > 1.0) opacity = 1.0;
          }
          
          col.push({ x, y, s, opacity });
        }
        points.push(col);
      }
      
      for (let i = 0; i < points.length; i++) {
        for (let j = 0; j < points[i].length; j++) {
          const p = points[i][j];
          
          if (currentMode === 'pluses') {
            ctx.strokeStyle = `rgba(198, 198, 203, ${p.opacity * 0.4})`;
            ctx.lineWidth = 1;
            ctx.beginPath();
            if (i < points.length - 1) {
              ctx.moveTo(p.x, p.y);
              ctx.lineTo(points[i+1][j].x, points[i+1][j].y);
            }
            if (j < points[i].length - 1) {
              ctx.moveTo(p.x, p.y);
              ctx.lineTo(points[i][j+1].x, points[i][j+1].y);
            }
            ctx.stroke();
            
            ctx.fillStyle = `rgba(198, 198, 203, ${p.opacity})`;
            ctx.beginPath();
            ctx.arc(p.x, p.y, p.s, 0, Math.PI * 2);
            ctx.fill();
          } else if (currentMode === 'rects_v') {
            ctx.fillStyle = `rgba(198, 198, 203, ${p.opacity})`; 
            ctx.beginPath();
            const startX = p.x - spacingX/2 + (spacingX * 0.05); 
            const h = 24;
            ctx.rect(startX, p.y - h/2, p.s, h);
            ctx.fill();
          } else {
            ctx.fillStyle = `rgba(198, 198, 203, ${p.opacity})`; 
            ctx.beginPath();
            if (currentMode === 'rects') {
              ctx.rect(p.x - p.s/2, p.y - p.s/2, p.s, p.s);
            } else {
              ctx.arc(p.x, p.y, p.s, 0, Math.PI * 2);
            }
            ctx.fill();
          }
        }
      }
      requestAnimationFrame(draw);
    }
    draw();
  }
});

// --- Components Search & Filter Logic ---
document.addEventListener('DOMContentLoaded', () => {
    const grid = document.querySelector('.components-grid');
    const searchInput = document.getElementById('component-search');
    const filtersContainer = document.getElementById('category-filters');
    
    if (!grid || !searchInput || !filtersContainer) return;

    // 1. Sort the components by category alphabetically
    const cards = Array.from(grid.querySelectorAll('.card'));
    
    cards.sort((a, b) => {
        const catA = (a.querySelector('.badge')?.textContent || '').trim().toLowerCase();
        const catB = (b.querySelector('.badge')?.textContent || '').trim().toLowerCase();
        if (catA < catB) return -1;
        if (catA > catB) return 1;
        // If categories are same, sort alphabetically by title
        const titleA = (a.querySelector('h3')?.textContent || '').trim().toLowerCase();
        const titleB = (b.querySelector('h3')?.textContent || '').trim().toLowerCase();
        if (titleA < titleB) return -1;
        if (titleA > titleB) return 1;
        return 0;
    });
    
    // Clear the grid and append sorted cards
    grid.innerHTML = '';
    cards.forEach(card => grid.appendChild(card));

    // 2. Extract unique categories and create filter buttons
    const categories = new Set();
    cards.forEach(card => {
        const badge = card.querySelector('.badge');
        if (badge) categories.add(badge.textContent.trim());
    });

    const sortedCategories = Array.from(categories).sort();
    
    // Create 'All' button
    const allBtn = document.createElement('button');
    allBtn.className = 'filter-btn active';
    allBtn.textContent = 'All';
    allBtn.dataset.category = 'all';
    filtersContainer.appendChild(allBtn);

    sortedCategories.forEach(cat => {
        const btn = document.createElement('button');
        btn.className = 'filter-btn';
        btn.textContent = cat;
        btn.dataset.category = cat;
        filtersContainer.appendChild(btn);
    });

    let currentCategory = 'all';
    
    // 3. Filter logic function
    function filterCards() {
        const searchTerm = searchInput.value.toLowerCase();
        
        cards.forEach(card => {
            const title = (card.querySelector('h3')?.textContent || '').toLowerCase();
            const desc = (card.querySelector('p')?.textContent || '').toLowerCase();
            const category = (card.querySelector('.badge')?.textContent || '').trim();
            
            const matchesSearch = title.includes(searchTerm) || desc.includes(searchTerm);
            const matchesCategory = currentCategory === 'all' || category === currentCategory;
            
            if (matchesSearch && matchesCategory) {
                card.style.display = 'flex';
            } else {
                card.style.display = 'none';
            }
        });
    }

    // Event listeners
    searchInput.addEventListener('input', filterCards);
    
    filtersContainer.addEventListener('click', (e) => {
        if (e.target.classList.contains('filter-btn')) {
            // Update active state
            document.querySelectorAll('.filter-btn').forEach(b => b.classList.remove('active'));
            e.target.classList.add('active');
            
            currentCategory = e.target.dataset.category;
            filterCards();
        }
    });
});
