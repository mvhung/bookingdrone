async function test() {
  const lat = 21.0428;
  const lon = 105.8145; // Click near H icon / Đốc Ngữ

  const overpassQuery = `[out:json][timeout:5];
    (
      nwr(around:150,${lat},${lon})["amenity"]["name"];
      nwr(around:150,${lat},${lon})["leisure"]["name"];
      nwr(around:150,${lat},${lon})["building"]["name"];
      nwr(around:150,${lat},${lon})["tourism"]["name"];
      nwr(around:150,${lat},${lon})["healthcare"]["name"];
      nwr(around:150,${lat},${lon})["office"]["name"];
    );
    out center 10;`;

  const endpoints = [
    'https://overpass-api.de/api/interpreter',
    'https://overpass.kumi.systems/api/interpreter',
    'https://maps.mail.ru/osm/tools/overpass/api/interpreter'
  ];

  for (const ep of endpoints) {
    try {
      console.log(`Testing endpoint: ${ep}`);
      const res = await fetch(ep, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/x-www-form-urlencoded',
          'User-Agent': 'EventMapProductionApp/1.0 (contact: support@eventmap.vn)'
        },
        body: 'data=' + encodeURIComponent(overpassQuery)
      }).then(r => r.json());

      console.log('Results count:', res.elements?.length);
      res.elements?.forEach((e, i) => {
        console.log(`[${i}] ${e.tags?.name} (amenity=${e.tags?.amenity}, building=${e.tags?.building}) center: [${e.lat || e.center?.lat}, ${e.lon || e.center?.lon}]`);
      });
      break;
    } catch (err) {
      console.log('Failed:', err.message);
    }
  }
}
test();
