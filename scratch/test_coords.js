async function test() {
  const lat = 21.0428;
  const lon = 105.8145;
  const url = `https://photon.komoot.io/reverse?lon=${lon}&lat=${lat}&limit=10`;
  const res = await fetch(url).then(r => r.json());
  res.features?.forEach((f, i) => {
    console.log(`[${i}] ${f.properties.name} (${f.properties.osm_key}=${f.properties.osm_value}) -> coords: [${f.geometry.coordinates[1]}, ${f.geometry.coordinates[0]}]`);
  });
}
test();
