async function resolvePlace(lat, lng) {
  const controller = new AbortController();
  const timeoutId = setTimeout(() => controller.abort(), 2000);

  const fetchPhoton = async () => {
    try {
      const url = `https://photon.komoot.io/reverse?lon=${lng}&lat=${lat}`;
      const res = await fetch(url, { signal: controller.signal }).then(r => r.json());
      if (res && res.features?.length > 0) {
        // Find best feature: POI over road
        const poiFeature = res.features.find(f => {
          const k = f.properties.osm_key;
          const name = f.properties.name;
          return name && (k === 'leisure' || k === 'amenity' || k === 'building' || k === 'tourism' || k === 'shop' || k === 'sport');
        });
        const f = poiFeature || res.features[0];
        const p = f.properties;
        const streetParts = [p.housenumber ? `${p.housenumber} ${p.street || ''}` : p.street, p.district, p.city].filter(Boolean);
        return {
          poiName: poiFeature ? poiFeature.properties.name : (p.osm_key !== 'highway' ? p.name : null),
          rawName: p.name,
          street: streetParts.join(', ') || p.street || '',
          allPois: res.features.map(x => x.properties.name).filter(Boolean)
        };
      }
    } catch {}
    return null;
  };

  const fetchNominatim = async () => {
    try {
      const url = `https://nominatim.openstreetmap.org/reverse?format=json&lat=${lat}&lon=${lng}&zoom=18&addressdetails=1`;
      const res = await fetch(url, {
        signal: controller.signal,
        headers: { 'User-Agent': 'EventMapVN/1.0', 'Accept-Language': 'vi' }
      }).then(r => r.json());
      if (res && res.address) {
        const addr = res.address;
        const venuePOI = 
          addr.amenity || addr.building || addr.hotel || addr.tourism || 
          addr.leisure || addr.shop || addr.commercial || addr.restaurant;
        
        const isRoadName = res.name && (res.class === 'highway' || res.addresstype === 'road' || res.name.startsWith('Ngõ') || res.name.startsWith('Đường') || res.name.startsWith('Phố'));
        const cleanPoi = (!isRoadName && res.name) ? res.name : venuePOI;

        const streetParts = [
          addr.house_number ? `${addr.house_number} ${addr.road || ''}` : addr.road,
          addr.suburb || addr.quarter,
          addr.city_district || addr.district || addr.county,
          addr.city || addr.state
        ].filter(Boolean);

        return {
          poiName: cleanPoi,
          street: streetParts.join(', '),
          rawName: res.name || streetParts[0]
        };
      }
    } catch {}
    return null;
  };

  try {
    const [photon, nom] = await Promise.all([fetchPhoton(), fetchNominatim()]);
    clearTimeout(timeoutId);

    // 1. HIGHEST PRIORITY: Genuine POI Venue name from Photon or Nominatim
    let locationName = photon?.poiName || nom?.poiName;

    // 2. If no explicit POI, check if rawName is NOT a pure road
    if (!locationName) {
      if (photon?.rawName && !photon.rawName.startsWith('Đường') && !photon.rawName.startsWith('Phố') && !photon.rawName.startsWith('Ngõ')) {
        locationName = photon.rawName;
      } else if (nom?.rawName && !nom.rawName.startsWith('Ngõ') && !nom.rawName.startsWith('Ngách')) {
        locationName = nom.rawName;
      }
    }

    // 3. Fallback to street or default
    if (!locationName) {
      locationName = nom?.street?.split(',')[0] || photon?.street?.split(',')[0] || 'Vị trí bản đồ';
    }

    const subAddress = nom?.street || photon?.street || 'Hà Nội';
    const nearbyPois = Array.from(new Set([
      ...(photon?.allPois || []),
      ...(nom?.poiName ? [nom.poiName] : [])
    ])).filter(p => p !== locationName);

    return { locationName, subAddress, nearbyPois };
  } catch (err) {
    return { locationName: 'Vị trí bản đồ', subAddress: 'Hà Nội', nearbyPois: [] };
  }
}

async function runTests() {
  const tests = [
    { name: 'Sân vận động Mỹ Đình', lat: 21.0205, lng: 105.7639 },
    { name: 'Trung tâm Hội nghị Quốc gia NCC', lat: 21.0055, lng: 105.7885 },
    { name: 'Tràng Tiền Plaza', lat: 21.0245, lng: 105.8525 },
    { name: 'Khách sạn Daewoo', lat: 21.0315, lng: 105.8125 }
  ];

  for (const t of tests) {
    const r = await resolvePlace(t.lat, t.lng);
    console.log(`\nPoint [${t.name}]`);
    console.log(' -> Location:', r.locationName);
    console.log(' -> SubAddress:', r.subAddress);
    console.log(' -> Nearby POIs:', r.nearbyPois);
  }
}

runTests();
