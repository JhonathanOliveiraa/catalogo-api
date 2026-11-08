// Carga do catalogo-api para o experimento de observabilidade do TCC.
//
// Cada iteração percorre o ciclo CRUD: cadastra, lista com filtros, consulta pelo id,
// edita (PUT) e alterna o status (PATCH). Não há DELETE na API: os produtos ficam no banco.
//
//   k6 run -e BASE_URL=https://catalogo-api.hml.exemplo.com scripts/carga.k6.js
//   k6 run -e BASE_URL=http://localhost:8080 -e VUS=5 -e DURACAO=1m scripts/carga.k6.js
import http from 'k6/http';
import { check, group, sleep } from 'k6';

const BASE_URL = (__ENV.BASE_URL || 'http://localhost:8080').replace(/\/+$/, '');
const VUS = Number(__ENV.VUS || 10);
const DURACAO = __ENV.DURACAO || '5m';

export const options = {
  scenarios: {
    crud: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '30s', target: VUS },
        { duration: DURACAO, target: VUS },
        { duration: '30s', target: 0 },
      ],
      gracefulRampDown: '10s',
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<500', 'p(99)<1000'],
    'http_req_duration{operacao:listar}': ['p(95)<400'],
    checks: ['rate>0.99'],
  },
};

const JSON_HEADERS = { headers: { 'Content-Type': 'application/json' } };

function json(res) {
  try {
    return res.json();
  } catch (_) {
    return null;
  }
}

export function setup() {
  const res = http.get(`${BASE_URL}/health`);
  if (res.status !== 200) {
    throw new Error(`readiness respondeu ${res.status}; o serviço não está pronto em ${BASE_URL}`);
  }
}

export default function () {
  const sufixo = `${__VU}-${__ITER}-${Date.now()}`;
  const nome = `k6 produto ${sufixo}`;
  let id;

  group('cadastrar', () => {
    const res = http.post(
      `${BASE_URL}/produtos`,
      JSON.stringify({ nome, descricao: 'gerado pelo k6', preco: 10 + (__ITER % 90), estoque: 5 }),
      { ...JSON_HEADERS, tags: { operacao: 'cadastrar' } },
    );
    const corpo = json(res);
    check(res, {
      'POST 201': (r) => r.status === 201,
      'POST Location': (r) => corpo !== null && r.headers.Location === `/produtos/${corpo.id}`,
      'POST nasce Ativo': () => corpo !== null && corpo.status === 'Ativo',
    });
    id = corpo && corpo.id;
  });

  if (!id) {
    sleep(1);
    return;
  }

  group('listar', () => {
    const query = 'nome=k6&precoMin=10&precoMax=100&status=Ativo&ordenarPor=Preco&direcao=Desc&page=1&pageSize=20';
    const res = http.get(`${BASE_URL}/produtos?${query}`, { tags: { operacao: 'listar', name: 'GET /produtos?filtros' } });
    const corpo = json(res);
    check(res, {
      'GET lista 200': (r) => r.status === 200,
      'GET lista paginada': () => corpo !== null && Array.isArray(corpo.itens) && corpo.page === 1 && corpo.pageSize === 20,
    });
  });

  group('consultar', () => {
    const res = http.get(`${BASE_URL}/produtos/${id}`, { tags: { operacao: 'consultar', name: 'GET /produtos/{id}' } });
    check(res, { 'GET id 200': (r) => r.status === 200 });
  });

  group('editar', () => {
    const res = http.put(
      `${BASE_URL}/produtos/${id}`,
      JSON.stringify({ nome: `${nome} (editado)`, descricao: null, preco: 99.9, estoque: 1 }),
      { ...JSON_HEADERS, tags: { operacao: 'editar', name: 'PUT /produtos/{id}' } },
    );
    const corpo = json(res);
    check(res, {
      'PUT 200': (r) => r.status === 200,
      'PUT descricao limpa': () => corpo !== null && corpo.descricao === null,
    });
  });

  group('status', () => {
    const desativar = http.patch(
      `${BASE_URL}/produtos/${id}/status`,
      JSON.stringify({ status: 'Desativado' }),
      { ...JSON_HEADERS, tags: { operacao: 'status', name: 'PATCH /produtos/{id}/status' } },
    );
    const corpo = json(desativar);
    check(desativar, {
      'PATCH 200': (r) => r.status === 200,
      'PATCH Desativado': () => corpo !== null && corpo.status === 'Desativado',
    });

    const reativar = http.patch(
      `${BASE_URL}/produtos/${id}/status`,
      JSON.stringify({ status: 'Ativo' }),
      { ...JSON_HEADERS, tags: { operacao: 'status', name: 'PATCH /produtos/{id}/status' } },
    );
    check(reativar, { 'PATCH reativa 200': (r) => r.status === 200 });
  });

  sleep(1);
}
