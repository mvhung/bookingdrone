async function test() {
  const lat = 21.0428;
  const lon = 105.8145; // Point near Bệnh viện Phổi
  const box = `${lon - 0.0015},${lat + 0.0015},${lon + 0.0015},${lat - 0.0015}`;

  // Test different queries:
  const queries = [
    '', // empty
    '*',
    'bệnh viện',
    '[amenity]',
    '[building]',
    'trung tâm',
    'khách sạn'
  ];

  for (const q of queries) {
    try {
      const url = `https://nominatim.openstreetmap.org/search?format=json&viewbox=${box}&bounded=1&q=${encodeURIComponent(q)}&limit=3`;
      const res = await fetch(url, {
        headers: { 'User-Agent': 'EventMapApp/1.0', 'Accept-Language': 'vi' }
      }).then(r => r.json());
      console.log(`Query "${q}" count:`, res?.length, res?.map(r => r.name));
    } catch (e) {
      console.log(`Query "${q}" error`);
    }
  }
}
test();
