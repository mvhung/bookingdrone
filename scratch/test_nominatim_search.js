async function test() {
  const queries = ['tiệc', 'tiệc cưới', 'trống đồng', 'sân vận động mỹ đình', 'vạn hoa', 'almaz', 'mỹ đình'];
  for (const q of queries) {
    const url = `https://nominatim.openstreetmap.org/search?format=json&q=${encodeURIComponent(q)}&countrycodes=vn&limit=4&addressdetails=1`;
    const res = await fetch(url, {
      headers: { 'User-Agent': 'EventMapVN/1.0', 'Accept-Language': 'vi' }
    }).then(r => r.json());
    console.log(`\n=== Search: "${q}" ===`);
    res.forEach((r, i) => {
      console.log(`[${i}] ${r.name} (${r.class}=${r.type}) -> ${r.display_name.slice(0, 70)}...`);
    });
  }
}
test();
