async function test() {
  const lat = 21.0428;
  const lon = 105.8145; // Click near H icon of Bệnh viện Phổi
  const url = `https://photon.komoot.io/reverse?lon=${lon}&lat=${lat}&limit=10`;
  const res = await fetch(url).then(r => r.json());
  console.log('Features count:', res.features?.length);
  res.features?.forEach((f, i) => {
    console.log(`[${i}] ${f.properties.name} (${f.properties.osm_key}=${f.properties.osm_value})`);
  });
}
test();
