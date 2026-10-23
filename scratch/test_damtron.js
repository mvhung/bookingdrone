async function test() {
  // Coordinates around Thụy Khuê / Văn Cao / Hồ Đầm Tròn
  // Let's test a grid around lat: 21.040 to 21.045, lon: 105.816 to 105.820
  for (let lat = 21.0420; lat <= 21.0440; lat += 0.001) {
    for (let lon = 105.8170; lon <= 105.8200; lon += 0.001) {
      const url = `https://photon.komoot.io/reverse?lon=${lon.toFixed(4)}&lat=${lat.toFixed(4)}`;
      const res = await fetch(url).then(r => r.json());
      const f = res.features?.[0];
      console.log(`(${lat.toFixed(4)}, ${lon.toFixed(4)}) -> ${f?.properties?.name} [${f?.properties?.osm_key}=${f?.properties?.osm_value}] street: ${f?.properties?.street}`);
    }
  }
}
test();
