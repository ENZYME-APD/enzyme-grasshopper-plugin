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
    
    let spacing = 16;
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
      maxSize = 20;  
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
      
      for (let gridX = spacing / 2; gridX < width + spacing; gridX += spacing) {
        for (let gridY = spacing / 2; gridY < height + spacing; gridY += spacing) {
          let x = gridX;
          let y = gridY;
          
          // Sinuous movement ONLY for pluses
          if (currentMode === 'pluses') {
            const offsetX = Math.sin(gridX * 0.015 + time * 1.2) * (spacing * 0.25);
            const offsetY = Math.cos(gridY * 0.015 + time * 0.9) * (spacing * 0.25);
            x += offsetX;
            y += offsetY;
          }
          
          const dx = mouse.x - x;
          const dy = mouse.y - y;
          const dist = Math.sqrt(dx * dx + dy * dy);
          
          let s = baseSize;
          let baseOpacity = 0.20; 
          let opacity = baseOpacity;
          
          if (dist < falloff) {
            const factor = 1 - (dist / falloff);
            const ease = 1 - Math.pow(1 - factor, 3);
            s = baseSize + (maxSize - baseSize) * ease;
            opacity = baseOpacity + (0.20 * ease); // Base 20% + up to 20% = 40% Max
          }
          
          if (currentMode === 'pluses') {
            const wave = Math.sin(x * 0.03 + time * 2.0) + Math.cos(y * 0.03 - time * 1.5);
            opacity += wave * 0.10; 
            if (opacity < 0.10) opacity = 0.10; 
            if (opacity > 1.0) opacity = 1.0;
            
            ctx.strokeStyle = `rgba(198, 198, 203, ${opacity})`;
            ctx.lineWidth = 1.5;
            ctx.beginPath();
            ctx.moveTo(x - s, y);
            ctx.lineTo(x + s, y);
            ctx.moveTo(x, y - s);
            ctx.lineTo(x, y + s);
            ctx.stroke();
          } else if (currentMode === 'rects_v') {
            ctx.fillStyle = `rgba(198, 198, 203, ${opacity})`; 
            ctx.beginPath();
            const startX = x - spacing/2 + (spacing * 0.05); 
            const h = 24;
            ctx.rect(startX, y - h/2, s, h);
            ctx.fill();
          } else {
            ctx.fillStyle = `rgba(198, 198, 203, ${opacity})`; 
            ctx.beginPath();
            if (currentMode === 'rects') {
              ctx.rect(x - s/2, y - s/2, s, s);
            } else {
              ctx.arc(x, y, s, 0, Math.PI * 2);
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
