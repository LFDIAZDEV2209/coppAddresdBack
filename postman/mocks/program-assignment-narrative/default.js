// Simulación LOCAL; fuente: ai-service/app/schemas/program_narrative.py.
// Sin llamadas a IA, datos clínicos reales ni persistencia.
const http = require('http');
const object = value => value !== null && typeof value === 'object' && !Array.isArray(value);
http.createServer((req, res) => {
  const send = (status, body) => {
    res.writeHead(status, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify(body));
  };
  if (req.method !== 'POST' || req.url !== '/internal/program/assignment-narrative')
    return send(501, { detail: 'Ruta fuera de esta simulación' });
  if (req.headers['x-internal-key'] !== 'simulation-only-key')
    return send(401, { detail: 'Clave interna inválida' });
  const body = req.body;
  const allowed = ['profile_snapshot', 'proposal', 'language', 'source_refs'];
  if (!object(body) || Object.keys(body).some(key => !allowed.includes(key)) ||
      !object(body.profile_snapshot) || !object(body.proposal) ||
      (body.language !== undefined && !['es', 'en'].includes(body.language)) ||
      !Array.isArray(body.source_refs) || body.source_refs.length > 100 ||
      body.source_refs.some(ref => typeof ref !== 'string' || ref.length < 1 || ref.length > 500 || !/^\S/.test(ref)) ||
      JSON.stringify(body.profile_snapshot).length > 100000 || JSON.stringify(body.proposal).length > 100000)
    return send(422, { detail: 'Contrato inválido: profile_snapshot, proposal y source_refs obligatorios' });
  if (req.headers['x-mock-response-code'] === '503')
    return send(503, { detail: 'Narrativa no disponible' });
  return send(200, { narratives: { es: 'Justificación clínica simulada.', en: 'Simulated clinical rationale.' } });
}).listen(process.env.PORT);
