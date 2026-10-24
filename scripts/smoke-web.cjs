const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright-core');
(async()=>{
 const browser=await chromium.launch({executablePath:process.env.CHROMIUM_EXECUTABLE || '/usr/bin/chromium',headless:true,args:['--no-sandbox']});
 try {
 const context=await browser.newContext({viewport:{width:1440,height:1000}});
 const page=await context.newPage();
 const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.goto((process.env.WEB_URL || 'http://localhost:5180')+'/dashboard');
 await page.getByRole('heading',{name:'Ravi de vous retrouver'}).waitFor();
 if (!page.url().endsWith('/login')) throw Error('Private route not redirected');
 await page.getByRole('link',{name:'Créer un compte',exact:true}).click();
 const id='Browser_'+Date.now();
 await page.getByLabel('Username',{exact:true}).fill(id);
 await page.getByLabel('Email',{exact:true}).fill(id+'@example.com');
 await page.getByLabel('Mot de passe',{exact:true}).fill('MyTCG-BrowserPassword42!');
 await page.getByLabel('Confirmer le mot de passe',{exact:true}).fill('MyTCG-BrowserPassword42!');
 await page.getByRole('button',{name:'Créer mon compte'}).click();
 await page.getByRole('heading',{name:'Ravi de vous retrouver'}).waitFor();
 await page.getByLabel('Email ou username',{exact:true}).fill(id);
 await page.getByLabel('Mot de passe',{exact:true}).fill('MyTCG-BrowserPassword42!');
 await page.getByRole('button',{name:/^Connexion/}).click();
 await page.getByRole('heading',{name:'Bienvenue, '+id+'.',exact:true}).waitFor();
 await page.screenshot({path:'/tmp/mytcg-dashboard.png',fullPage:true});
 if(await page.locator('.coming').count()!==4)throw Error('Missing future features');
 const cookie=(await context.cookies()).find(c=>c.name==='mytcg.refresh');
 if(!cookie?.httpOnly || cookie.sameSite!=='Strict')throw Error('Cookie flags missing');
 if(await page.evaluate(()=>Object.keys(localStorage).length)!==0) throw Error('localStorage not empty');
 await page.reload();
 await page.getByRole('heading',{name:'Bienvenue, '+id+'.',exact:true}).waitFor();
 // Preserve cookies into a fresh browser context, simulating browser restart.
 const state=await context.storageState(); const restarted=await browser.newContext({storageState:state,viewport:{width:390,height:844}});
 const mobile=await restarted.newPage();await mobile.goto((process.env.WEB_URL || 'http://localhost:5180')+'/');
 await mobile.getByRole('heading',{name:'Bienvenue, '+id+'.',exact:true}).waitFor();
 if(await mobile.evaluate(()=>document.documentElement.scrollWidth>innerWidth))throw Error('Mobile horizontal overflow');
 await mobile.screenshot({path:'/tmp/mytcg-mobile-web.png',fullPage:true});
 await mobile.getByRole('button',{name:'Déconnexion',exact:true}).click();
 await mobile.getByRole('heading',{name:'Ravi de vous retrouver'}).waitFor();
 await mobile.reload();await mobile.getByRole('heading',{name:'Ravi de vous retrouver'}).waitFor();
 await mobile.screenshot({path:'/tmp/mytcg-login.png',fullPage:true});
 if(errors.length)throw Error(errors.join(';'));
 console.log('PASS browser: private-route redirect, registration, login, dashboard, HTTP-only cookie, no localStorage, reload, browser restart, responsive 390px, logout.');
 } finally { await browser.close(); }
})().catch(e=>{console.error(e);process.exit(1)});
