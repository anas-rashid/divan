// Make an existing account an admin, from the server (the first admin cannot be made from the site).
// The person signs up on the site first, then:
//   node src/admin-cli.ts admin@example.com
import { pool } from './db.ts';
import { audit } from './admin.ts';

const email = (process.argv[2] ?? '').trim().toLowerCase();
if (!email) {
  console.error('usage: node src/admin-cli.ts <email of an existing account>');
  process.exit(1);
}
const { rows } = await pool.query(`UPDATE users SET role = 'admin', disabled_at = NULL WHERE email = $1 RETURNING id, email`, [email]);
if (rows[0]) {
  await audit(null, 'promote', rows[0], { to: 'admin', via: 'server' });
  console.log(`${email} is now an admin`);
} else console.error(`no account with ${email}; sign up on the site first`);
await pool.end();
process.exit(rows[0] ? 0 : 1);
