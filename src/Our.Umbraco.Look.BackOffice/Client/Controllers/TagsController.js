﻿(function () {
    'use strict';

    angular
        .module('umbraco')
        .controller('Look.BackOffice.TagsController', TagsController);

    TagsController.$inject = ['$scope', '$routeParams', 'Look.BackOffice.ApiService'];

    function TagsController($scope, $routeParams, apiService) {

        $scope.searcherName = $routeParams.id;

        apiService.getViewDataForTags($scope.searcherName)
            .then(function (response) {

                $scope.response = response.data;

            });


        apiService
            .getMatches($scope.searcherName) // TODO: rename to GetTagMatches ?
            .then(function (response) {

                $scope.matchesResponse = response.data;

            });
    }

})();