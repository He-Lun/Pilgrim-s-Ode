#include <bits/stdc++.h>
using namespace std;

static vector<int> build(int n) {
    if (n == 1) {
        return {1};
    }
    if (n == 2) {
        return {1, 2};
    }

    vector<int> ans = {1};
    int lo = 2;
    int hi;
    int next_big = -1;

    if (n & 1) {
        ans.push_back(n);
        hi = n - 1;
    } else {
        ans.push_back(n - 1);
        hi = n - 2;
        next_big = n;
    }

    while (lo <= hi || next_big != -1) {
        if (lo <= hi) {
            ans.push_back(lo++);
        }
        if (next_big != -1) {
            ans.push_back(next_big);
            next_big = -1;
        } else if (lo <= hi) {
            ans.push_back(hi--);
        }
    }

    return ans;
}

int main() {
    ios::sync_with_stdio(false);
    cin.tie(nullptr);

    int T;
    cin >> T;
    while (T--) {
        int n;
        cin >> n;
        vector<int> ans = build(n);
        for (int i = 0; i < n; i++) {
            if (i) {
                cout << ' ';
            }
            cout << ans[i];
        }
        cout << '\n';
    }
    return 0;
}
