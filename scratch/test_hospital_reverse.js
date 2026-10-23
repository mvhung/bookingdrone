async function test() {
  // Let's test Nominatim reverse with addressdetails=1 around 21.042 to 21.044, 105.813 to 105.816
  const points = [
    { name: 'Point A (Near H icon)', lat: 21.0428, lon: 105.8145 },
    { name: 'Point B (Near text)', lat: 21.0425, lon: 105.8138 },
    { name: 'Point C (Thụy Khuê gate)', lat: 21.0432, lon: 105.8152 },
    { name: 'Point D (Hoàng Hoa Thám gate)', lat: 21.0418, lon: 105.8130 }
  ];

  for (const pt of points) {
    const url = `https://nominatim.openstreetmap.org/reverse?format=json&lat=${pt.lat}&lon=${pt.lon}&zoom=18&addressdetails=1`;
    try {
      const res = await fetch(url, {
        headers: { 'User-Agent': 'EventMapVNApp/1.0', 'Accept-Language': 'vi' }
      }).then(r => r.json());
      console.log(`\n=== ${pt.name} (${pt.lat}, ${pt.lon}) ===`);
      console.log('name:', res.name);
      console.log('type:', res.type, 'class:', res.class);
      console.log('display_name:', res.display_name);
      console.log('address:', res.address);
    } catch (e) {
      console.error(e);
    }
  }
}
test();
