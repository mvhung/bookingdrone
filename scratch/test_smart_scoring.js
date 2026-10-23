async function test() {
  const testPoints = [
    { name: 'Bệnh viện Phổi (H icon on map tile)', lat: 21.0428, lon: 105.8145 },
    { name: 'Sân vận động Mỹ Đình', lat: 21.0205, lon: 105.7639 },
    { name: 'Trung tâm Hội nghị Quốc gia', lat: 21.0055, lon: 105.7885 },
    { name: 'Khách sạn Daewoo', lat: 21.0315, lon: 105.8125 },
    { name: 'Trường Đại học Quốc Gia (Xuân Thủy)', lat: 21.0375, lon: 105.7815 }
  ];

  const VENUE_KEYWORDS = [
    'bệnh viện', 'viện', 'trường', 'đại học', 'sân vận động', 'nhà thi đấu', 
    'trung tâm', 'hội nghị', 'tiệc cưới', 'khách sạn', 'hotel', 'resort', 
    'plaza', 'building', 'tower', 'tòa nhà', 'chùa', 'nhà thờ', 'công viên', 
    'bảo tàng', 'cung văn hóa', 'nhà hát'
  ];

  function getPoiScore(f) {
    const p = f.properties;
    const name = (p.name || '').toLowerCase();
    const k = p.osm_key;
    const v = p.osm_value;

    let score = 0;
    // Check keyword
    if (VENUE_KEYWORDS.some(kw => name.includes(kw))) {
      score += 50;
    }
    // Check category
    if (k === 'amenity' || k === 'healthcare') score += 30;
    if (k === 'leisure' || k === 'tourism') score += 25;
    if (k === 'building' && v !== 'yes') score += 20;
    if (k === 'shop' || k === 'office') score += 10;
    if (k === 'highway') {
      if (v === 'bus_stop' && VENUE_KEYWORDS.some(kw => name.includes(kw))) {
        score += 35; // Bus stops named after major hospitals/stadiums!
      } else {
        score -= 20; // Pure roads
      }
    }
    return score;
  }

  for (const pt of testPoints) {
    const url = `https://photon.komoot.io/reverse?lon=${pt.lon}&lat=${pt.lat}&limit=10`;
    const res = await fetch(url).then(r => r.json());
    
    // Sort features by score
    const scoredFeatures = (res.features || []).map(f => ({
      feature: f,
      score: getPoiScore(f)
    })).sort((a, b) => b.score - a.score);

    const best = scoredFeatures[0]?.score > 0 ? scoredFeatures[0].feature : res.features?.[0];
    const bestProp = best?.properties;

    // Clean name: if it's a bus stop named "Bệnh viện Phổi Trung ương", keep the clean venue name!
    let venueName = bestProp?.name;
    if (venueName && venueName.startsWith('Điểm dừng ')) {
      venueName = venueName.replace('Điểm dừng ', '');
    }

    console.log(`\n=== Click: ${pt.name} (${pt.lat}, ${pt.lon}) ===`);
    console.log('-> Resolved POI Name:', venueName);
    console.log('-> Category:', `${bestProp?.osm_key}=${bestProp?.osm_value}`);
    console.log('-> Snapped Coords:', best?.geometry?.coordinates ? [best.geometry.coordinates[1], best.geometry.coordinates[0]] : 'None');
    console.log('-> Top 3 candidates:', scoredFeatures.slice(0, 3).map(x => `${x.feature.properties.name} (${x.score}pts)`));
  }
}
test();
