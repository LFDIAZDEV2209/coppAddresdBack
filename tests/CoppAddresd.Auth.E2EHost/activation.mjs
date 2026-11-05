/** Full app -> real local Auth/Identity -> isolated PostgreSQL; Twilio HTTP is synthetic. */
import {createRequire} from 'node:module';
import {execFileSync} from 'node:child_process';
import {writeFile} from 'node:fs/promises';
import path from 'node:path';
import assert from 'node:assert/strict';
const app=process.env.COP_E2E_APP_DIR??path.resolve('../antares-paciente');
const require=createRequire(path.join(app,'package.json'));
const {chromium}=require('playwright');const {expect}=require('playwright/test');
const out=process.env.COP_E2E_OUTPUT,db=process.env.COP_E2E_DATABASE,auth=process.env.COP_E2E_AUTH_URL;
assert(out?.startsWith('/tmp/'));assert(/^copp_review_[a-z0-9_]+$/.test(db??''));assert.equal(auth,'http://127.0.0.1:5125');
const sql=query=>execFileSync('docker',['--context','default','exec','copp-qa-postgres-20261013','psql','-U','postgres','-d',db,'-v','ON_ERROR_STOP=1','-Atc',query],{encoding:'utf8'}).trim();
const browser=await chromium.launch(),results=[];
const scenarios=[['phone-only','QA-OTP-NO-EMAIL','123456'],['expired-code','QA-OTP-EXPIRED','000000'],['wrong-code','QA-OTP-WRONG','999999'],['invalid-saved-email','QA-OTP-BAD-EMAIL','123456']];
for(const [scenario,document,code] of scenarios.filter(row=>!process.env.COP_E2E_SCENARIO||row[0]===process.env.COP_E2E_SCENARIO)){
 // Solo datos sintéticos de la base protegida; cada ejecución vuelve a probar el alta, no un usuario ya creado.
 sql(`DO $$ DECLARE previous uuid; BEGIN SELECT user_id INTO previous FROM app.patient_profiles WHERE document_number='${document}'; UPDATE app.patient_profiles SET user_id=null WHERE document_number='${document}'; IF previous IS NOT NULL THEN DELETE FROM auth."Users" WHERE "Id"=previous; END IF; END $$;`);
 const context=await browser.newContext({viewport:{width:390,height:950}}),page=await context.newPage();let verification;
 await page.route('**/api/**',async route=>{const request=route.request(),p=new URL(request.url()).pathname;
  if(p.startsWith('/api/auth/')){const response=await route.fetch({url:auth+p,timeout:15000});return route.fulfill({response});}
  if(p==='/api/v1/me/patient-profile')return route.fulfill({json:{patientId:'55555555-5555-4555-8555-555555555501',firstName:'Paciente',lastName:'QA',documentNumber:document,email:'',phone:'2025550101',emergencyName:'',emergencyPhone:'',emergencyRelationship:'',emergencyEmail:'',secondaryEmergencyContact:null,insurerId:null,memberId:''}});
  return route.fulfill({json:[]});
 });
 try{
  await page.goto(process.env.COP_E2E_APP_URL??'http://127.0.0.1:5173');
  await page.getByRole('button',{name:'Activa tu cuenta',exact:true}).click();await page.locator('#login-first-id input').fill(document);await page.getByRole('button',{name:'Confirmar identidad',exact:true}).click();
  await page.getByRole('radio').filter({hasText:'SMS'}).click();await page.locator('#login-otp-0').fill(code);
  const pending=page.waitForResponse(response=>new URL(response.url()).pathname==='/api/auth/verify-otp'&&response.request().method()==='POST');
  await page.getByRole('button',{name:'Verificar y entrar',exact:true}).click();const response=await pending;const body=await response.json();verification={status:response.status(),detail:body.detail};
  if(scenario==='phone-only'){
   assert.equal(response.status(),200,JSON.stringify(verification));await expect(page.getByRole('heading',{name:'Tus datos',exact:true})).toBeVisible();
   const row=JSON.parse(sql(`SELECT json_build_object('emailMissing',u."Email" IS NULL,'phoneVerified',u."PhoneNumberConfirmed",'emailVerified',u."EmailConfirmed",'username',u."UserName",'phone',u."PhoneNumber",'userId',u."Id") FROM auth."Users" u JOIN app.patient_profiles p ON p.user_id=u."Id" WHERE p.document_number='${document}';`));
   assert.equal(row.emailMissing,true);assert.equal(row.phoneVerified,true);assert.equal(row.emailVerified,false);assert.match(row.username,/^patient-[a-f0-9]{32}$/);assert.equal(row.phone,'+12025550101');const jwt=JSON.parse(Buffer.from(body.accessToken.split('.')[1],'base64url').toString());assert.equal(jwt.sub??jwt.nameid??jwt['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'],row.userId);assert(!('email' in jwt));assert(!('http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress' in jwt));
  }else if(scenario==='expired-code'){
   assert.equal(response.status(),400,JSON.stringify(verification));await expect(page.getByText('Este código ya se usó o venció. Solicita uno nuevo.',{exact:false})).toBeVisible();
  }else if(scenario==='wrong-code')assert.equal(response.status(),401,JSON.stringify(verification));
  else assert.equal(response.status(),400,JSON.stringify(verification));
  if(scenario!=='phone-only')assert.equal(sql(`SELECT count(*) FROM app.patient_profiles WHERE document_number='${document}' AND user_id IS NOT NULL;`),'0');
  await page.screenshot({path:path.join(out,`${scenario}.png`),animations:'disabled'});results.push({scenario,result:'passed',status:response.status()});
 }catch(error){results.push({scenario,result:'failed',error:error.message,verification});await page.screenshot({path:path.join(out,`${scenario}-failure.png`),animations:'disabled'});}
 await context.close();
}
await browser.close();await writeFile(path.join(out,'activation-results.json'),JSON.stringify(results,null,2));console.log(JSON.stringify(results));if(results.some(r=>r.result==='failed'))process.exitCode=1;
