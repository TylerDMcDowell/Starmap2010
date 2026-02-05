BEGIN;

-- ============================================================
-- Ael-Ruun system seed (Astraeum Protectorate)
-- Generated UTC: 2026-01-26 01:03:09Z
-- Schema target: StarInfo.db.sql
-- ============================================================

-- --- Governments (minimal, for FK integrity)
INSERT OR IGNORE INTO governments (government_id, name, short_name, description, color_hex)
VALUES
  ('ced3ebb4-a8d8-439c-8ee1-243775ac0232', 'Astraeum Protectorate', 'Astraeum', 'Protectorate polity; Luminoti-steeped civic theology; territorial and procedurally rigid.', '#2E6F7E'),
  ('92f46e0c-5e9c-49f0-b454-fa084d7859bc', 'Solarian Concord', 'Solaria', 'Solarian successor polity; expeditionary military tradition; post-war influence network.', '#D0A34B'),
  ('3d3728b4-9160-4d73-8559-0e727f5b83fb', 'Thalean Houses Compact', 'Thaleans', 'Loose compact of Thalean noble houses; prestige politics and patronage.', '#6B4FA1');

-- --- Star system
INSERT INTO star_systems (
  system_id, sector, region,
  system_name, real_system_name,
  distance_ly, x, y, z, ra_deg, dec_deg,
  screen_x, screen_y,
  government_id, population_total, star_count,
  system_type, has_stations, has_gates,
  notes, strategic_value, habitability_class
) VALUES (
  '8b963937-9d06-46fd-b5d2-84626084a73f', 'Outer March', 'Astraeum Protectorate',
  'Ael-Ruun', 'Ael-Ruun',
  74.2, 112.4, -38.7, 19.2, 312.445, -12.33,
  812, 455,
  'ced3ebb4-a8d8-439c-8ee1-243775ac0232', 99000000, 1,
  'Single', 1, 1,
  'Protectorate-held system. Visitors are screened by doctrine-as-procedure; mistakes are treated as moral failures.', 4, 'Habitable'
);

-- --- System star(s)
INSERT INTO system_stars (
  system_id, star_name, real_star_name,
  star_type, spectral_class,
  mass_solar, radius_solar, luminosity_solar,
  temperature_k, age_gyr, notes
) VALUES (
  '8b963937-9d06-46fd-b5d2-84626084a73f', 'Ael-Ruun', 'Ael-Ruun',
  'Main Sequence', 'K2V',
  0.78, 0.75, 0.30,
  4900, 6.1,
  'Orange dwarf; long-lived, stable output. Protectorate liturgy treats the star as a “steadfast witness” in civic ritual.'
);

-- ============================================================
-- System objects (planets + moons)
-- ============================================================

-- Planet 1: Cinder (inner scorched rock)
INSERT INTO system_objects (object_id, system_id, object_type, object_name, orbit_host_object_id, orbit_position, is_primary, notes)
VALUES ('1eec8aae-0063-4544-a37f-1fe635a3bf8d', '8b963937-9d06-46fd-b5d2-84626084a73f', 'planet', 'Cinder', NULL, 1, 0,
        'Tidally-resonant scorchworld; used for sensor calibration rites and as a “purity” reference body.');

INSERT INTO planet_details (
  object_id,
  radius_km, mass_earth, gravity_g,
  atmosphere_type, climate, biome,
  semi_major_axis_au, orbital_period_days, day_length_hours,
  axial_tilt_deg, avg_temp_k, water_fraction,
  population, primary_settlement,
  major_exports, major_imports, notes
) VALUES (
  '1eec8aae-0063-4544-a37f-1fe635a3bf8d',
  3100, 0.11, 0.34,
  'Trace', 'Hot', 'Barren',
  0.28, 74.3, 19.2,
  4.0, 620, 0.00,
  1200, 'Cinder Station (subsurface)',
  'High-temp ceramics; doped sensor substrates', 'Volatiles; specialty alloys',
  'Personnel rotate under vows of silence during calibration cycles.'
);

-- Planet 2: Aureline (bonded port / screening world)
INSERT INTO system_objects (object_id, system_id, object_type, object_name, orbit_host_object_id, orbit_position, is_primary, notes)
VALUES ('3a7bf37a-0922-4d0d-abed-28f4de28d0c6', '8b963937-9d06-46fd-b5d2-84626084a73f', 'planet', 'Aureline', NULL, 2, 0,
        'Low-density world with a long-established bonded-port enclave. Outsiders are processed here to keep them away from inner holy traffic.');

INSERT INTO planet_details VALUES (
  '3a7bf37a-0922-4d0d-abed-28f4de28d0c6',
  5200, 0.52, 0.78,
  'Thin', 'Temperate', 'Steppe/Ocean Patches',
  0.62, 214.0, 26.4,
  13.0, 286, 0.08,
  680000, 'Aureline Bonded Port',
  'Bonded logistics; certified spares; gate-stamp services', 'Medical reagents; food concentrates',
  'Customs staff trained as lay-adepts; doctrine audits embedded in manifests.'
);

-- Planet 3: Sanctum (habitable; Protectorate center)
INSERT INTO system_objects (object_id, system_id, object_type, object_name, orbit_host_object_id, orbit_position, is_primary, notes)
VALUES ('9f0719cf-ba20-4445-9b8a-5e87b3794fcc', '8b963937-9d06-46fd-b5d2-84626084a73f', 'planet', 'Sanctum', NULL, 3, 1,
        'Primary Protectorate population center in-system; civic theology embedded into transit law, architecture, and signal protocol.');

INSERT INTO planet_details VALUES (
  '9f0719cf-ba20-4445-9b8a-5e87b3794fcc',
  6410, 1.02, 1.01,
  'Breathable', 'Temperate', 'Mixed Forest/Sea',
  0.94, 362.0, 24.8,
  22.5, 292, 0.66,
  98000000, 'Lumenward City',
  'Doctrine-grade encryption; navigational ephemerides; cultural exports', 'Rare isotopes; deep-field sensors',
  'Power exercised via clerical bureaus; belief audited as behavior, not confession.'
);

-- Planet 4: Thren (outer gas giant)
INSERT INTO system_objects (object_id, system_id, object_type, object_name, orbit_host_object_id, orbit_position, is_primary, notes)
VALUES ('f62b14b5-d1c8-45db-a170-60ca9b337cf1', '8b963937-9d06-46fd-b5d2-84626084a73f', 'planet', 'Thren', NULL, 4, 0,
        'Outer gas giant; approach-screen for covert arrivals. Rings + magnetosphere create noisy sensor geometry.');

INSERT INTO planet_details VALUES (
  'f62b14b5-d1c8-45db-a170-60ca9b337cf1',
  64000, 85.0, 2.3,
  'H/He', 'Cold', 'GasGiant',
  4.9, 4010.0, 10.7,
  2.0, 140, 0.00,
  0, NULL,
  'Helium-3 skimming; deuterium separation', 'Crew consumables; replacement baffles',
  'Protected orbital zones; patrol attention biased toward the main gate vector.'
);

-- --- Moons of Thren
INSERT INTO system_objects (object_id, system_id, object_type, object_name, orbit_host_object_id, orbit_position, is_primary, notes)
VALUES ('55f8fbb4-1692-4be0-ad4f-1daf2a575871', '8b963937-9d06-46fd-b5d2-84626084a73f', 'moon', 'Ashveil', 'f62b14b5-d1c8-45db-a170-60ca9b337cf1', 1, 0,
        'Industrial skimming hub. Loud, dirty, full of contractors with quiet oaths.');
INSERT INTO moon_details VALUES (
  '55f8fbb4-1692-4be0-ad4f-1daf2a575871',
  1800, 0.03, 0.19,
  'Thin', 'Cold', 'Barren',
  0.0042, 3.1, 38.1,
  0.5, 140, 0.00,
  240000, 'Ashveil Skimworks',
  'He-3; deuterium', 'Parts; medical; fuel catalysts',
  'Mixed crews; Protectorate oversight is strict but transactional.'
);

INSERT INTO system_objects (object_id, system_id, object_type, object_name, orbit_host_object_id, orbit_position, is_primary, notes)
VALUES ('51a06a9a-68a1-4c8d-ac2b-d8d2fc169ab5', '8b963937-9d06-46fd-b5d2-84626084a73f', 'moon', 'Reliquary', 'f62b14b5-d1c8-45db-a170-60ca9b337cf1', 2, 0,
        'Ice moon in a declared quiet zone. Officially a preservation site; unofficially a place people stop asking questions.');
INSERT INTO moon_details VALUES (
  '51a06a9a-68a1-4c8d-ac2b-d8d2fc169ab5',
  2400, 0.06, 0.27,
  'Trace', 'Cryo', 'Ice/Subsurface Ocean',
  0.0068, 5.6, 52.0,
  0.1, 110, 0.40,
  9000, 'Reliquary Ward',
  'Cryogenic isotopes (licensed); archival substrates', 'Everything (by permit only)',
  'Access requires liturgical clearance; patrols treat curiosity as suspicious intent.'
);

INSERT INTO system_objects (object_id, system_id, object_type, object_name, orbit_host_object_id, orbit_position, is_primary, notes)
VALUES ('b3075b85-792b-466f-b6d0-5abab986318e', '8b963937-9d06-46fd-b5d2-84626084a73f', 'moon', 'Vesper', 'f62b14b5-d1c8-45db-a170-60ca9b337cf1', 3, 0,
        'Captured irregular; high-inclination orbit. Useful as a blind-spot anchor for people who know where to look.');
INSERT INTO moon_details VALUES (
  'b3075b85-792b-466f-b6d0-5abab986318e',
  620, 0.002, 0.07,
  'None', 'Cryo', 'Rock',
  0.0115, 12.2, 9.2,
  14.0, 90, 0.00,
  0, NULL,
  'None', 'None',
  'Listed as “unremarkable debris” in public registries; internal nav charts flag it as a hazard.'
);

-- ============================================================
-- Installations (AUTOINCREMENT installation_id)
-- ============================================================

-- 1) Aureline Bonded Customs Complex
INSERT INTO system_installations (system_id, installation_name, installation_type, owner_government_id, is_primary, strategic_role, notes)
VALUES ('8b963937-9d06-46fd-b5d2-84626084a73f', 'Aureline Bonded Customs Complex', 'CustomsPort', 'ced3ebb4-a8d8-439c-8ee1-243775ac0232', 1, 'Screening / Paperwork Control',
        'Where “welcome” is decided. If you are allowed inward, it is stamped here first.');
INSERT INTO installation_details (installation_id, host_object_id, orbit_notes, security_level, garrison_strength, services, notes)
VALUES (last_insert_rowid(), '3a7bf37a-0922-4d0d-abed-28f4de28d0c6', 'Equatorial elevator + bonded ring', 'High', 'Medium',
        'Docking; bonded warehousing; certified spares; med clinic; comm relays',
        'Approach challenges are layered as call-and-response liturgy; noncompliance is treated as intent.');

-- 2) Lumenward Signal Cathedral
INSERT INTO system_installations (system_id, installation_name, installation_type, owner_government_id, is_primary, strategic_role, notes)
VALUES ('8b963937-9d06-46fd-b5d2-84626084a73f', 'Lumenward Signal Cathedral', 'CommsArray', 'ced3ebb4-a8d8-439c-8ee1-243775ac0232', 1, 'System Comms / Doctrine Broadcast',
        'Massive phased array that doubles as civic ritual: announcements, ephemerides, and “confirmations of order.”');
INSERT INTO installation_details (installation_id, host_object_id, orbit_notes, security_level, garrison_strength, services, notes)
VALUES (last_insert_rowid(), '9f0719cf-ba20-4445-9b8a-5e87b3794fcc', 'Ground-based megastructure; uplinked to orbital repeaters', 'VeryHigh', 'High',
        'Encrypted comms; navigation ephemerides; identity verification',
        'Transponders are validated as much by posture and phrasing as by cryptography.');

-- 3) Reliquary Quiet Ward (caretaker-adjacent decoy site)
INSERT INTO system_installations (system_id, installation_name, installation_type, owner_government_id, is_primary, strategic_role, notes)
VALUES ('8b963937-9d06-46fd-b5d2-84626084a73f', 'Reliquary Quiet Ward', 'ResearchPreserve', 'ced3ebb4-a8d8-439c-8ee1-243775ac0232', 0, 'Restricted Preserve / Black Archive',
        'Declared preservation site. Official story: ecology + archaeology. Unofficial story: some things are safer when nobody owns them.');
INSERT INTO installation_details (installation_id, host_object_id, orbit_notes, security_level, garrison_strength, services, notes)
VALUES (last_insert_rowid(), '51a06a9a-68a1-4c8d-ac2b-d8d2fc169ab5', 'Subsurface vault network; thermal masking; low-emission ops', 'VeryHigh', 'Low',
        'Minimal docking; quarantine bay; encrypted local storage',
        'A plausible hiding place for a “physical caretaker machine” — which is exactly why it works as a misdirection.');

-- ============================================================
-- Jump gate
-- ============================================================
INSERT INTO jump_gates (gate_id, system_id, gate_name, gate_type, location_object_id, status, notes)
VALUES ('c9c2e9ee-0ee5-4a08-8c94-52f90f0db5cd', '8b963937-9d06-46fd-b5d2-84626084a73f', 'Thren Outer Gate', 'Standard', 'f62b14b5-d1c8-45db-a170-60ca9b337cf1', 'Operational',
        'Main lane is patrolled; outer approach via Thren’s magnetosphere is noisy and can conceal a skilled pilot.');

COMMIT;