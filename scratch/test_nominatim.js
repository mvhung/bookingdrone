async function test() {
  for (const zoom of [18, 17, 16, 15, 14]) {
    const url = `https://nominatim.openstreetmap.org/reverse?format=json&lat=21.0205&lon=105.7639&zoom=${zoom}&addressdetails=1`;
    const res = await fetch(url, {
      headers: { 'User-Agent': 'EventMapVN/1.0', 'Accept-Language': 'vi' }
    }).then(r => r.json());
    console.log(`Zoom ${zoom}:`, {
      name: res.name,
      display_name: res.display_name,
      type: res.type,
      class: res.class,
      category: res.category,
      addresstype: res.addresstype,
      address: res.address
    });
  }
}
test();
