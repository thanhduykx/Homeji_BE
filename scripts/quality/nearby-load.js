import http from 'k6/http';
import { check, sleep } from 'k6';

const baseUrl = __ENV.BASE_URL || 'http://127.0.0.1:5189';
if (!/^http:\/\/127\.0\.0\.1:\d+$/.test(baseUrl)) {
  throw new Error('This benchmark is restricted to a local disposable API.');
}

export const options = {
  scenarios: { nearby: { executor: 'constant-vus', vus: 5, duration: '30s' } },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    'http_req_duration{endpoint:nearby}': ['p(95)<500'],
    checks: ['rate==1'],
  },
};

export default function () {
  const response = http.get(`${baseUrl}/api/marketplace-posts?listingType=2&latitude=10.85&longitude=106.77&radiusKm=3&pageSize=20`,
    { tags: { endpoint: 'nearby' } });
  let posts;
  try { posts = response.json(); } catch { posts = null; }
  check(response, {
    'HTTP 200': r => r.status === 200,
    'food catalog returned': () => Array.isArray(posts) && posts.length > 0 && posts.length <= 20,
    'only food within radius': () => Array.isArray(posts) && posts.every(p => p.listingType === 2 && p.distanceKm <= 3),
    'ordered by distance': () => Array.isArray(posts) && posts.every((p, i) => i === 0 || posts[i - 1].distanceKm <= p.distanceKm),
  });
  sleep(0.15);
}
