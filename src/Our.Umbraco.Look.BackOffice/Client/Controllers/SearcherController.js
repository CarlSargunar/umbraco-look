﻿(function () {
    'use strict';

    angular
        .module('umbraco')
        .controller('Look.BackOffice.SearcherController', SearcherController);

    SearcherController.$inject = ['$scope', '$routeParams', 'Look.BackOffice.ApiService', '$q'];

    function SearcherController($scope, $routeParams, apiService, $q) {

        // input params
        $scope.searcherName = $routeParams.id;
        $scope.searcherDescription = null;
        $scope.searcherType = null;
        $scope.icon = null;

        // view data
        apiService.getViewDataForSearcher($scope.searcherName)
            .then(function (response) {

                $scope.searcherDescription = response.data.SearcherDescription;
                $scope.searcherType = response.data.SearcherType;
                $scope.icon = response.data.Icon;

                $scope.viewData = response.data;
        });

        // matches
        $scope.getMatches = function (skip, take) {

            var q = $q.defer();

            apiService
                .getMatches($scope.searcherName)
                .then(function (response) {
                    q.resolve(response.data.Matches);
                });

            return q.promise;
        };

    }

})();