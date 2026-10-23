async function test() {
  // Let's test a few coordinates around Bệnh viện Phổi Trung ương (463 Hoàng Hoa Thám, Ba Đình, Hà Nội)
  // Search Nominatim for "Bệnh viện Phổi Trung ương" first to get its exact coordinate:
  const searchUrl = 'https://nominatim.openstreetmap.org/search?format=json&q=Bệnh viện Phổi Trung ương&countrycodes=vn&limit=1';
  const searchRes = await fetch(searchUrl, {
    headers: { 'User-Agent': 'EventMapApp', 'Accept-Language': 'vi' }
  }).then(r => r.json());
  console.log('Search result:', searchRes[0]);

  if (searchRes[0]) {
    const lat = parseFloat(searchRes[0].lat);
    const lon = parseFloat(searchRes[0].lon);
    console.log(`\nTesting reverse at exact POI (${lat}, ${lon}):`);
    
    // Test Photon reverse
    const pUrl = `https://photon.komoot.io/reverse?lon=${lon}&lat=${lat}`;
    const pRes = await fetch(pUrl).then(r => r.json());
    console.log('Photon features at exact point:');
    pRes.features?.slice(0, 3).forEach((f, i) => console.log(`[${i}]`, f.properties.name, `(${f.properties.osm_key}=${f.properties.osm_value})`));

    // Test reverse at a point 30m, 50m, 80m away (where someone might click on the building or icon):
    const offsets = [
      { label: '30m north', lat: lat + 0.0003, lon: lon },
      { label: '50m east', lat: lat, lon: lon + 0.0005 },
      { label: '80m south', lat: lat - 0.0007, lon: lon }
    ];
    for (const off of offsets) {
      console.log(`\nTesting offset ${off.label} (${off.lat}, ${off.lon}):`);
      const pUrlOff = `https://photon.komoot.io/reverse?lon=${off.lon}&lat=${off.lat}`;
      const pResOff = await fetch(pUrlOff).then(r => r.json());
      pResOff.features?.slice(0, 3).forEach((f, i) => console.log(`[${i}]`, f.properties.name, `(${f.properties.osm_key}=${f.properties.osm_value})`));
    }
  }
}
test();
