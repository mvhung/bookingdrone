async function test() {
  const coords = [
    { name: 'Mỹ Đình Center', lat: 21.0205, lon: 105.7639 },
    { name: 'Mỹ Đình Plaza/Square', lat: 21.0205, lon: 105.7665 },
    { name: 'Trống Đồng Cảnh Hồ', lat: 20.9995, lon: 105.8198 },
    { name: 'JW Marriott', lat: 21.0068, lon: 105.7839 }
  ];
  for (const c of coords) {
    const url = `https://photon.komoot.io/reverse?lon=${c.lon}&lat=${c.lat}`;
    const res = await fetch(url).then(r => r.json());
    console.log(`\n=== ${c.name} ===`);
    res.features?.slice(0, 3).forEach((f, i) => {
      console.log(`[${i}]`, f.properties.name, `(${f.properties.osm_key}=${f.properties.osm_value})`, '| Street:', f.properties.street);
    });
  }
}
test();
