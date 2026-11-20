import http from 'k6/http';
import { check, sleep } from 'k6';

// Adresse de l'application (modifiable : k6 run -e BASE_URL=http://localhost:8080 ...)
const BASE_URL = __ENV.BASE_URL || 'http://localhost:5284';

const VILLES = ['Tunis', 'Sousse', 'Sfax', 'Monastir', 'Bizerte', 'Nabeul', 'Kairouan', 'Gabès'];

// EQ-PERF-01 : 20 utilisateurs simultanés, p95 < 500 ms
export const options = {
  stages: [
    { duration: '30s', target: 20 }, // montée progressive jusqu'à 20 utilisateurs
    { duration: '1m',  target: 20 }, // 20 utilisateurs pendant 1 minute
    { duration: '15s', target: 0 },  // descente
  ],
  thresholds: {
    http_req_duration: ['p(95)<500'], // 95 % des requêtes en moins de 500 ms
    http_req_failed: ['rate<0.01'],   // moins de 1 % d'erreurs
    checks: ['rate>0.99'],
  },
};

export default function () {
  const ville = VILLES[Math.floor(Math.random() * VILLES.length)];
  const page = 1 + Math.floor(Math.random() * 20);

  // 1. Page de recherche (5000 trajets en base)
  let r = http.get(`${BASE_URL}/Trip`, { tags: { name: 'Recherche - accueil' } });
  check(r, { 'accueil : statut 200': (x) => x.status === 200 });
  sleep(1);

  // 2. Filtre par ville de départ
  r = http.get(`${BASE_URL}/Trip?departure=${encodeURIComponent(ville)}`,
               { tags: { name: 'Recherche - filtre ville' } });
  check(r, { 'filtre : statut 200': (x) => x.status === 200 });
  sleep(1);

  // 3. Navigation dans les pages de résultats
  r = http.get(`${BASE_URL}/Trip?page=${page}`, { tags: { name: 'Recherche - pagination' } });
  check(r, { 'pagination : statut 200': (x) => x.status === 200 });
  sleep(1);
}