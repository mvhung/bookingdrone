async function test() {
  const url = `https://photon.komoot.io/api/?q=tiệc&bbox=102.1,8.1,109.5,23.4&limit=5`;
  const res = await fetch(url).then(r => r.json());
  console.log('Photon with VN bbox for "tiệc":', res.features?.map(f => ({ name: f.properties.name, street: f.properties.street, city: f.properties.city })));
}
test();
