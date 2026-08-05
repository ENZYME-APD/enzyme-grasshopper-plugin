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
    
    let spacing, baseSize, maxSize, falloff;
    if (currentMode === 'pluses') {
      spacing = 32;
      baseSize = 4;
      maxSize = 4; 
      falloff = 250;
    } else if (currentMode === 'rects') {
      spacing = 16;
      baseSize = 4;
      maxSize = 12;
      falloff = 200;
    } else if (currentMode === 'rects_v') {
      spacing = 24;
      baseSize = spacing * 0.2; // 0.2 of spacing
      maxSize = spacing * 0.8;  // 0.8 of spacing
      falloff = 200;
    } else { // dots
      spacing = 12;
      baseSize = 1.5;
      maxSize = baseSize * 1.5;
      falloff = 200;
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
      
      for (let x = spacing / 2; x < width; x += spacing) {
        for (let y = spacing / 2; y < height; y += spacing) {
          const dx = mouse.x - x;
          const dy = mouse.y - y;
          const dist = Math.sqrt(dx * dx + dy * dy);
          
          let s = baseSize;
          let opacity = currentMode === 'pluses' ? 0.05 : 0.20;
          
          if (dist < falloff) {
            const factor = 1 - (dist / falloff);
            const ease = 1 - Math.pow(1 - factor, 3);
            if (currentMode !== 'pluses') {
              s = baseSize + (maxSize - baseSize) * ease;
            }
            opacity = (currentMode === 'pluses' ? 0.05 : 0.20) + ((currentMode === 'pluses' ? 0.5 : 0.25) * ease); 
          }
          
          if (currentMode === 'pluses') {
            const noise = Math.sin(x * 0.01 + time) * Math.cos(y * 0.01 + time * 1.2) * Math.sin((x+y)*0.02 - time*0.8);
            if (noise > 0.8) {
               opacity += (noise - 0.8) * 1.5; 
            }
            ctx.strokeStyle = `rgba(198, 198, 203, ${opacity})`;
            ctx.lineWidth = 1;
            ctx.beginPath();
            ctx.moveTo(x - s, y);
            ctx.lineTo(x + s, y);
            ctx.moveTo(x, y - s);
            ctx.lineTo(x, y + s);
            ctx.stroke();
          } else if (currentMode === 'rects_v') {
            ctx.fillStyle = `rgba(198, 198, 203, ${opacity})`; 
            ctx.beginPath();
            const startX = x - spacing/2 + (spacing * 0.1); 
            // 1/3 proportion at base size: baseSize is spacing*0.2, so height is spacing*0.6
            const h = spacing * 0.6;
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
