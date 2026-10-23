async function test() {
  const lat = 21.0428;
  const lon = 105.8145; // Point A (near H icon, which returned Phố Đốc Ngữ in reverse)

  // Test 1: Photon search with lat/lon and small radius
  console.log('\n--- Test 1: Photon with q="" or q=* ---');
  // Photon /reverse actually returns multiple features sorted by distance!
  const pUrl = `https://photon.komoot.io/reverse?lon=${lon}&lat=${lat}`;
  const pRes = await fetch(pUrl).then(r => r.json());
  console.log('Photon features count:', pRes.features?.length);
  pRes.features?.forEach((f, i) => {
    console.log(`[${i}] ${f.properties.name} (${f.properties.osm_key}=${f.properties.osm_value})`);
  });

  // Test 2: Overpass query for amenity, leisure, building, tourism within 120m
  console.log('\n--- Test 2: Overpass API around 120m ---');
  try {
    const overpassQuery = `[out:json][timeout:4];
      (
        nwr(around:120,${lat},${lon})["amenity"]["name"];
        nwr(around:120,${lat},${lon})["leisure"]["name"];
        nwr(around:120,${lat},${lon})["tourism"]["name"];
        nwr(around:120,${lat},${lon})["healthcare"]["name"];
      );
      out center 5;`;
    const oRes = await fetch('https://overpass.kumi.systems/api/interpreter', {
      method: 'POST',
      body: 'data=' + encodeURIComponent(overpassQuery)
    }).then(r => r.json());
    console.log('Overpass results:', oRes.elements?.map(e => ({
      name: e.tags?.name,
      amenity: e.tags?.amenity,
      lat: e.lat || e.center?.lat,
      lon: e.lon || e.center?.lon
    })));
  } catch (e) {
    console.error('Overpass error:', e.message);
  }

  // Test 3: Nominatim search bounded to bounding box
  console.log('\n--- Test 3: Nominatim bounding box ---');
  try {
    const box = `${lon - 0.0015},${lat + 0.0015},${lon + 0.0015},${lat - 0.0015}`;
    const nUrl = `https://nominatim.openstreetmap.org/search?format=json&viewbox=${box}&bounded=1&q=bệnh viện&limit=3`;
    const nRes = await fetch(nUrl, {
      headers: { 'User-Agent': 'EventMapApp', 'Accept-Language': 'vi' }
    }).then(r => r.json());
    console.log('Nominatim bbox results:', nRes.map(x => ({ name: x.name, display_name: x.display_name, lat: x.lat, lon: x.lon })));
  } catch (e) {
    console.error('Nominatim error:', e.message);
  }
}
test();
