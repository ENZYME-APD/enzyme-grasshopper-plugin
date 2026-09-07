const fs = require('fs');
const https = require('https');
const path = require('path');
const sharp = require('./script-env/node_modules/sharp');

const args = process.argv.slice(2);
if (args.length === 0) {
    console.error("Usage: node get_icon.js <icon-name> [<icon-name2> ...]");
    process.exit(1);
}

const resourcesDir = path.join(__dirname, 'Resources');
if (!fs.existsSync(resourcesDir)) {
    fs.mkdirSync(resourcesDir);
}

async function fetchAndConvert(iconName) {
    const url = `https://unpkg.com/lucide-static@latest/icons/${iconName}.svg`;
    
    return new Promise((resolve, reject) => {
        https.get(url, (res) => {
            if (res.statusCode >= 300 && res.statusCode < 400 && res.headers.location) {
                const redirectUrl = res.headers.location.startsWith('http') ? res.headers.location : `https://unpkg.com${res.headers.location}`;
                https.get(redirectUrl, (res2) => {
                    handleResponse(res2, iconName, resolve, reject);
                });
            } else {
                handleResponse(res, iconName, resolve, reject);
            }
        }).on('error', reject);
    });
}

function handleResponse(res, iconName, resolve, reject) {
    if (res.statusCode !== 200) {
        reject(new Error(`Failed to fetch ${iconName} (Status: ${res.statusCode})`));
        return;
    }
    
    let svgData = '';
    res.on('data', chunk => svgData += chunk);
    res.on('end', async () => {
        try {
            const pngPath = path.join(__dirname, 'Resources', `${iconName}.png`);
            
            // Standardizing the stroke color to dark gray for GH icons and making it slightly thicker for visibility
            const styledSvg = svgData.replace(/currentColor/g, '#333333').replace(/stroke-width="2"/, 'stroke-width="2.5"');
            
            await sharp(Buffer.from(styledSvg))
                .resize(24, 24)
                .png()
                .toFile(pngPath);
                
            console.log(`[SUCCESS] Downloaded and converted ${iconName} to Resources/${iconName}.png`);
            resolve(pngPath);
        } catch (err) {
            reject(err);
        }
    });
}

async function run() {
    for (const name of args) {
        try {
            await fetchAndConvert(name);
        } catch(e) {
            console.error(`[ERROR] ${name}: ${e.message}`);
        }
    }
}
run();
