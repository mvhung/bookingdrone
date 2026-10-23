async function test() {
  const q = `[out:json][timeout:5];(
    nwr(around:250,21.0205,105.7639)["name"]["leisure"];
    nwr(around:250,21.0205,105.7639)["name"]["amenity"];
    nwr(around:250,21.0205,105.7639)["name"]["building"];
    nwr(around:250,21.0205,105.7639)["name"]["tourism"];
  );out tags 10;`;
  const res = await fetch('https://overpass-api.de/api/interpreter', {
    method: 'POST',
    body: 'data=' + encodeURIComponent(q)
  }).then(r => r.json());
  console.log('Overpass Elements:', JSON.stringify(res.elements?.map(e => e.tags), null, 2));
}
test();
