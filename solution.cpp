#include <bits/stdc++.h>
using namespace std;

int main() {
    ios::sync_with_stdio(false);
    cin.tie(nullptr);

    int n, m;
    cin >> n >> m;

    const int M = 1 << m;
    vector<vector<long long>> d(n, vector<long long>(m));
    vector<int> reject(n, 0);

    for (int i = 0; i < n; i++) {
        for (int j = 0; j < m; j++) {
            cin >> d[i][j];
        }
        string s;
        cin >> s;
        for (int j = 0; j < m; j++) {
            if (s[j] == 'R') {
                reject[i] |= 1 << j;
            }
        }
    }

    // alive_cost[j][mask] = sum of d[i][j] over programs still running after mask
    vector<vector<long long>> alive_cost(m, vector<long long>(M, 0));

    for (int j = 0; j < m; j++) {
        vector<long long> h(M, 0);
        for (int i = 0; i < n; i++) {
            h[reject[i]] += d[i][j];
        }

        // SOS: h[mask] = sum over reject-bitmasks that are subsets of mask
        for (int bit = 0; bit < m; bit++) {
            for (int mask = 0; mask < M; mask++) {
                if (mask & (1 << bit)) {
                    h[mask] += h[mask ^ (1 << bit)];
                }
            }
        }

        for (int mask = 0; mask < M; mask++) {
            alive_cost[j][mask] = h[(M - 1) ^ mask];
        }
    }

    const long long INF = (1LL << 62);
    vector<long long> dp(M, INF);
    dp[0] = 0;

    for (int mask = 0; mask < M; mask++) {
        if (dp[mask] == INF) {
            continue;
        }
        for (int j = 0; j < m; j++) {
            if (mask & (1 << j)) {
                continue;
            }
            int nmask = mask | (1 << j);
            dp[nmask] = min(dp[nmask], dp[mask] + alive_cost[j][mask]);
        }
    }

    cout << dp[M - 1] << '\n';
    return 0;
}
